using System.Collections.Generic;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries
{
    public class LoadProductsQuery : CatalogQueryBase<LoadProductResponse>
    {
        public IList<string> ObjectIds { get; set; }
        public bool EvaluatePromotions { get; set; } = true;
    }
}
