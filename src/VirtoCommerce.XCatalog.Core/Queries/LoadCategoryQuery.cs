using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries
{
    public class LoadCategoryQuery : CatalogQueryBase<LoadCategoryResponse>
    {
        public string[] ObjectIds { get; set; }
    }
}
