using System.Collections.Generic;

namespace VirtoCommerce.XCatalog.Core.Models
{
    public class LoadProductResponse
    {
        public LoadProductResponse(ICollection<ExpProduct> expProducts)
        {
            Products = expProducts;
        }

        public ICollection<ExpProduct> Products { get; private set; }
    }
}
