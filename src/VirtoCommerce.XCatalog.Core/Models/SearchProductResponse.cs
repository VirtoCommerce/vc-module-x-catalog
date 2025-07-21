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
        public IList<SearchProductFilterResult> Filters { get; set; }

        public IEnumerable<Currency> AllStoreCurrencies { get; set; }
        public Currency Currency { get; set; }
        public Store Store { get; set; }
    }

    public class SearchProductFilterResult
    {
        public string Name { get; set; }
        public string Label { get; set; }

        /// <summary>
        /// "term" or "range"
        /// </summary>
        public string FilterType { get; set; }

        public IList<SearchProductFilterTermValue> TermValues { get; set; } = [];
        public IList<SearchProductFilterRangeValue> RangeValues { get; set; } = [];
    }

    public class SearchProductFilterTermValue
    {
        public string Value { get; set; }
    }

    public class SearchProductFilterRangeValue
    {
        public object Lower { get; set; }
        public object Upper { get; set; }

        public bool IncludeLowerBound { get; set; }
        public bool IncludeUpperBound { get; set; }
    }
}
