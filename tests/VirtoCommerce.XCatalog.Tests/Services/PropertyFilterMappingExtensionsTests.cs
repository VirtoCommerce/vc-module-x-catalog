using System;
using System.Collections.Generic;
using FluentAssertions;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.XCatalog.Data.Services;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Services;

public class PropertyFilterMappingExtensionsTests
{
    private readonly IXCatalogMapper _mapper = new XCatalogMapper();

    [Fact]
    public void MapTo_TermFilter_SetsMatchingProperty()
    {
        var filters = new List<IFilter> { new TermFilter { FieldName = "catalogId", Values = ["catalog-1"] } };
        var criteria = new PropertySearchCriteria();

        _mapper.MapTo(filters, criteria);

        criteria.CatalogId.Should().Be("catalog-1");
    }

    [Fact]
    public void MapTo_NullFilters_DoesNotThrow()
    {
        var criteria = new PropertySearchCriteria();

        FluentActions.Invoking(() => _mapper.MapTo(null, criteria)).Should().NotThrow();
    }

    [Fact]
    public void MapTo_NullCriteria_Throws()
    {
        FluentActions.Invoking(() => _mapper.MapTo(new List<IFilter>(), null)).Should().Throw<ArgumentNullException>();
    }
}
