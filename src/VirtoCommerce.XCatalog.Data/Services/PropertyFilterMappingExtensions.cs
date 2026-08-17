using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Index;

namespace VirtoCommerce.XCatalog.Data.Services;

public static class PropertyFilterMappingExtensions
{
    public static void MapTo(this IList<IFilter> filters, PropertySearchCriteria criteria)
    {
        if (filters == null || criteria == null)
        {
            return;
        }

        foreach (var term in filters.OfType<TermFilter>())
        {
            term.MapTo(criteria);
        }
    }
}
