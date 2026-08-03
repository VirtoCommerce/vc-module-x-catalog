using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.XCatalog.Data.Index;
using VirtoCommerce.XCatalog.Data.Queries;

namespace VirtoCommerce.XCatalog.Benchmark.Caching;

// What a request that searches ONCE pays for the deduplication it never uses: BuildSearchCacheKey
// serializes the whole SearchRequest and hashes it, and that cost is charged on every call with no hit to
// amortise it. The platform's JsonHashBenchmarks measure the hashing utility on a synthetic tree, because
// Platform.Core cannot reference a search module; these measure the shipped method on a request built the
// way GetIndexedSearchRequestBuilder builds it.
//
// The fixtures mirror that method's call chain rather than picking builder calls that look representative.
// A thinner graph does not fail — it silently reports a smaller number, and the first version of this file
// did exactly that: it omitted AddCertainDateFilter (unconditional in production, and the whole reason the
// certain-date pinning exists) and ApplyMultiSelectFacetSearch (which clones the filter tree onto EVERY
// aggregation), understating by 1.6x and 3-4.5x respectively.
//
// Two production steps are deliberately NOT reproduced, and both make these figures a floor rather than an
// estimate: ParseFilters / ParseFacets need an ISearchPhraseParser and add user filters on top, and the
// module pipeline (_pipeline.Execute(builder)) lets other modules extend the request before it is built.
//
// There is no baseline arm: the alternative to building a key is not building one, so the absolute figure
// IS the overhead. Read it against an Elasticsearch round-trip (milliseconds) to judge the trade.

/// <summary>
/// Exposes the protected key builder. <c>GetType()</c> therefore reports this class rather than the
/// handler, which changes the key's type segment but not what the measurement costs — the segment is
/// rendered through a per-type cache either way. Dependencies are null because the method touches none of
/// them, and the ten-argument base call binds to the current constructor, not to either obsolete overload.
/// </summary>
internal sealed class KeyBuilder : SearchProductQueryHandler
{
    public KeyBuilder()
        : base(null, null, null, null, null, null, null, null, null, null)
    {
    }

    public string Build(SearchRequest searchRequest) => BuildSearchCacheKey(searchRequest);
}

internal static class RequestFixture
{
    // Production pins one instant per request; a fixed one here keeps the benchmark deterministic.
    private static readonly DateTime _certainDate = new(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

    // IndexFieldsMapper.MapToIndexIncludes turns the GraphQL selection into dotted leaf paths, so a
    // realistic set is neither three literals nor the whole document.
    private static readonly string[] _includeFields =
    [
        "__object.id", "__object.code", "__object.name", "__object.categoryId", "__object.outlines",
        "__object.images", "__object.properties", "__prices.list", "__prices.sale", "__prices.currency",
        "__variations.id", "__variations.code",
    ];

    /// <summary>
    /// The calls <c>GetIndexedSearchRequestBuilder</c> makes on every request. A subsequence of its chain,
    /// not a prefix: production interleaves <c>ParseFilters</c> and <c>WithSearchPhrase</c> between these,
    /// and each arm appends its own. Order is immaterial to the built graph — every call here writes a
    /// different part of the request — but do not read this as "nothing was interleaved".
    /// </summary>
    public static IndexSearchRequestBuilder Common()
    {
        return new IndexSearchRequestBuilder()
            .WithStoreId("B2B-store")
            .WithUserId("11111111-1111-1111-1111-111111111111")
            .WithOrganizationId("22222222-2222-2222-2222-222222222222")
            .WithCatalog("catalog-id")
            .WithCurrency("USD")
            .WithFuzzy(false)
            .AddCertainDateFilter(_certainDate)
            .WithMultilanguageProperties(["Brand", "Color"])
            .WithCultureName("en-US")
            .WithPreserveUserQuery(false)
            .WithIncludeFields(_includeFields);
    }

    /// <summary>
    /// What <c>AddDefaultTerms</c> adds — production applies it only when the request carries no object ids.
    /// </summary>
    public static IndexSearchRequestBuilder AddDefaultTerms(this IndexSearchRequestBuilder builder)
    {
        builder.AddTermFilter("is", "product", skipIfExists: true);
        builder.AddTermFilter("status", "visible", skipIfExists: true);
        builder.AddTermFilter("__outline", "catalog-id");

        return builder;
    }
}

/// <summary>
/// The by-ids load: the shape that actually deduplicates, since the same product set is re-requested by
/// several resolvers within one GraphQL request. Carries no aggregations — the response clone is free on
/// this path, so the key is the only cost the deduplication adds.
/// </summary>
[MemoryDiagnoser]
public class SearchCacheKeyByIdsBenchmarks
{
    private readonly KeyBuilder _keyBuilder = new();
    private SearchRequest _request;

    [Params(1, 20, 100)]
    public int ObjectIdCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var ids = Enumerable.Range(0, ObjectIdCount).Select(i => $"product-{i:D8}").ToArray();

        // No default terms: production skips them when the request carries object ids.
        _request = RequestFixture.Common()
            .WithPaging(0, ObjectIdCount)
            .AddObjectIds(ids)
            .Build();
    }

    [Benchmark]
    public string BuildKey() => _keyBuilder.Build(_request);
}

/// <summary>
/// The browse shape: keyword, catalog and validity-window filters, sorting and facets. It deduplicates less
/// often, but it is the largest request the key is taken over.
/// <para>
/// Specifically a catalog-ROOT browse: <c>AddDefaultTerms</c> contributes a single-segment
/// <c>__outline</c>, for which production also leaves <c>CategoryId</c> null and emits one priority
/// sorting field. A category browse is strictly larger — a longer outline value, which
/// <c>ApplyMultiSelectFacetSearch</c> then clones into every aggregation, plus a fourth sorting field —
/// and it reaches the request through <c>ParseFilters</c>, which this fixture does not reproduce. So the
/// figures stay a floor in that direction too.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class SearchCacheKeyBrowseBenchmarks
{
    private readonly KeyBuilder _keyBuilder = new();
    private SearchRequest _request;

    [Params(4, 16)]
    public int FacetCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var builder = RequestFixture.Common()
            .WithSearchPhrase("safety gloves")
            .AddSorting("priority:desc;name:asc")
            .WithPaging(0, 20)
            .AddDefaultTerms();

        foreach (var i in Enumerable.Range(0, FacetCount))
        {
            builder.Aggregations.Add(new TermAggregationRequest
            {
                Id = $"facet-{i}",
                FieldName = $"property-{i}",
                Size = 10,
                Values = [$"value-{i}-a", $"value-{i}-b"],
            });
        }

        // Load-bearing, and the omission the first version of this file shipped: production calls this
        // immediately after ParseFacets, and it attaches a filtered CLONE of the whole request filter tree
        // to every aggregation. Without it the hashed graph is 3-4.5x smaller than anything production has.
        _request = builder
            .ApplyMultiSelectFacetSearch()
            .Build();
    }

    [Benchmark]
    public string BuildKey() => _keyBuilder.Build(_request);
}
