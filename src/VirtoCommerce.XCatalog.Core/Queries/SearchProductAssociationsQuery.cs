using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries
{
    public class SearchProductAssociationsQuery : CatalogQueryBase<SearchProductAssociationsResponse>
    {
        public string[] ObjectIds { get; set; }
        public string Keyword { get; set; }
        public string Group { get; set; }
        public string Sort { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }
}
