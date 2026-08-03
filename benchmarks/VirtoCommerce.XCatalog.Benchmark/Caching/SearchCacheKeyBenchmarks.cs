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
// Platform.Core cannot reference a search module; these measure the shipped method on a request built by
// the same IndexSearchRequestBuilder the handler uses, which is the number a reviewer actually asked for.
//
// There is no baseline arm: the alternative to building a key is not building one, so the absolute figure
// IS the overhead. Read it against an Elasticsearch round-trip (milliseconds) to judge the trade.

/// <summary>
/// Exposes the protected key builder. <c>GetType()</c> therefore reports this class rather than the
/// handler, which changes the key's type segment but not what the measurement costs — the segment is
/// rendered the same way either way. Dependencies are null because the method touches none of them.
/// </summary>
internal sealed class KeyBuilder : SearchProductQueryHandler
{
    public KeyBuilder()
        : base(null, null, null, null, null, null, null, null, null, null)
    {
    }

    public string Build(SearchRequest searchRequest) => BuildSearchCacheKey(searchRequest);
}

/// <summary>
/// The by-ids load: the shape that actually deduplicates, since the same product set is re-requested by
/// several resolvers within one GraphQL request. Carries no aggregations, which is why the response clone
/// is free on this path and the key is the only cost the dedup adds.
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

        _request = new IndexSearchRequestBuilder()
            .WithStoreId("B2B-store")
            .WithIncludeFields("__object", "__prices", "__variations")
            .AddObjectIds(ids)
            .WithPaging(0, ObjectIdCount)
            .Build();
    }

    [Benchmark]
    public string BuildKey() => _keyBuilder.Build(_request);
}

/// <summary>
/// The browse shape: keyword, catalog and validity-window filters, sorting and facets. It does not
/// deduplicate as often, but it is the biggest request the key is taken over, so it brackets the cost from
/// above.
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
        var builder = new IndexSearchRequestBuilder()
            .WithStoreId("B2B-store")
            .WithSearchPhrase("safety gloves")
            .WithIncludeFields("__object", "__prices")
            .AddTermFilter("__outline", "catalog-id/category-id")
            .AddCertainDateFilter(new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc))
            .AddSorting("priority:desc;name:asc")
            .WithPaging(0, 20);

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

        _request = builder.Build();
    }

    [Benchmark]
    public string BuildKey() => _keyBuilder.Build(_request);
}
