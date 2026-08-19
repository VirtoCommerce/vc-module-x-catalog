using System.Linq;
using AutoMapper;
using FluentAssertions;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XCatalog.Data.Services;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Mappers;

public class MappingTermFilterTests
{
    private static readonly IMapper _legacyMapper = new MapperConfiguration(cfg => cfg.AddProfile(new LegacyFacetMappingProfile())).CreateMapper();
    private static readonly IXCatalogMapper _mapper = new XCatalogMapper();

    [Fact]
    public void ToFacetResult_TermAggregation_MatchesLegacyMapper()
    {
        var source = new Aggregation
        {
            AggregationType = "attr",
            Field = "color",
            Items =
            [
                new AggregationItem { Count = 1, Value = "red", IsApplied = true },
                new AggregationItem { Count = 2, Value = "blue", IsApplied = false },
            ],
        };

        var legacy = _legacyMapper.Map<FacetResult>(source, options => options.Items["cultureName"] = "en-US") as TermFacetResult;

        var actual = _mapper.ToFacetResult(source, new FacetMappingContext { CultureName = "en-US" }) as TermFacetResult;

        actual.Should().NotBeNull();
        actual.Name.Should().Be(legacy.Name);
        actual.Label.Should().Be(legacy.Label);
        actual.Terms.Select(x => (x.Term, x.Count, x.IsSelected, x.Label))
            .Should().BeEquivalentTo(legacy.Terms.Select(x => (x.Term, x.Count, x.IsSelected, x.Label)), o => o.WithStrictOrdering());
    }

    [Fact]
    public void ToFacetResult_RangeAggregation_MatchesLegacyMapper()
    {
        var source = new Aggregation
        {
            AggregationType = "range",
            Field = "price",
            Items =
            [
                new AggregationItem
                {
                    Count = 5,
                    Value = "0-100",
                    IsApplied = true,
                    RequestedLowerBound = "0",
                    RequestedUpperBound = "100",
                    IncludeLower = true,
                    IncludeUpper = false,
                },
            ],
            Statistics = new AggregationStatistics { Min = 0, Max = 999 },
        };

        var legacy = _legacyMapper.Map<FacetResult>(source, options => options.Items["cultureName"] = "en-US") as RangeFacetResult;

        var actual = _mapper.ToFacetResult(source, new FacetMappingContext { CultureName = "en-US" }) as RangeFacetResult;

        actual.Should().NotBeNull();
        actual.Name.Should().Be(legacy.Name);
        actual.Label.Should().Be(legacy.Label);
        actual.Statistics.Min.Should().Be(legacy.Statistics.Min);
        actual.Statistics.Max.Should().Be(legacy.Statistics.Max);
        actual.Ranges.Select(x => (x.From, x.To, x.FromStr, x.ToStr, x.IncludeFrom, x.IncludeTo, x.Count, x.IsSelected, x.Label))
            .Should().BeEquivalentTo(legacy.Ranges.Select(x => (x.From, x.To, x.FromStr, x.ToStr, x.IncludeFrom, x.IncludeTo, x.Count, x.IsSelected, x.Label)));
    }

    [Fact]
    public void ToFacetResult_UnknownAggregationType_ReturnsNull_MatchesLegacyMapper()
    {
        var source = new Aggregation { AggregationType = "unknown", Field = "color" };

        var legacy = _legacyMapper.Map<FacetResult>(source, options => options.Items["cultureName"] = "en-US");
        var actual = _mapper.ToFacetResult(source, new FacetMappingContext { CultureName = "en-US" });

        actual.Should().BeNull();
        legacy.Should().BeNull();
    }

    [Fact]
    public void ToFacetResult_NullSource_ReturnsNull()
    {
        _mapper.ToFacetResult(null, new FacetMappingContext { CultureName = "en-US" }).Should().BeNull();
    }
}
