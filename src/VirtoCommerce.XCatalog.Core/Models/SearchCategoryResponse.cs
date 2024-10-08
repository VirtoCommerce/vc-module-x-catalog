using System.Collections.Generic;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Core.Models
{
    public class SearchCategoryResponse
    {
        public SearchCategoryQuery Query { get; set; }

        public int TotalCount { get; set; }
        public IList<ExpCategory> Results { get; set; }
        public Store Store { get; set; }
    }
}
