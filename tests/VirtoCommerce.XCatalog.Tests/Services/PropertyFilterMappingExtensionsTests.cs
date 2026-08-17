using System.Collections.Generic;
using FluentAssertions;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.XCatalog.Data.Services;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Services;

public class PropertyFilterMappingExtensionsTests
{
    [Fact]
    public void MapTo_TermFilter_SetsMatchingProperty()
    {
        var filters = new List<IFilter> { new TermFilter { FieldName = "catalogId", Values = ["catalog-1"] } };
        var criteria = new PropertySearchCriteria();

        filters.MapTo(criteria);

        criteria.CatalogId.Should().Be("catalog-1");
    }

    [Fact]
    public void MapTo_NullFiltersOrCriteria_DoesNotThrow()
    {
        var criteria = new PropertySearchCriteria();

        ((List<IFilter>)null).MapTo(criteria);
        new List<IFilter>().MapTo(null);
    }
}
