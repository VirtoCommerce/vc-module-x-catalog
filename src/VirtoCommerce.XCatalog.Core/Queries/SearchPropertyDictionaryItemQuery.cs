using System.Collections.Generic;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries
{
    public class SearchPropertyDictionaryItemQuery : CatalogQueryBase<SearchPropertyDictionaryItemResponse>
    {
        public int Skip { get; set; }
        public int Take { get; set; }

        public IList<string> PropertyIds { get; set; }
    }
}
