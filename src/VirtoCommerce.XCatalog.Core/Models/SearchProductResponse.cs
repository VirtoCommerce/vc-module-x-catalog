using System.Collections.Generic;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Core.Models
{
    public class SearchProductResponse
    {
        public SearchProductQuery Query { get; set; }

        public int TotalCount { get; set; }
        public IList<ExpProduct> Results { get; set; }
        public IList<FacetResult> Facets { get; set; }

        public IEnumerable<Currency> AllStoreCurrencies { get; set; }
        public Currency Currency { get; set; }
        public Store Store { get; set; }
    }
}
