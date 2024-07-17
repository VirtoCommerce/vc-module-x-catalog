using System;
using System.Linq;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Specifications
{
    public class CatalogProductIsBuyableSpecification
    {
        /// <summary>
        /// Evaluates a product is buyable specification.
        /// </summary>
        /// <param name="product"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual bool IsSatisfiedBy(ExpProduct product)
        {
            ArgumentNullException.ThrowIfNull(product);

            return product.IndexedProduct.IsActive.GetValueOrDefault(false)
                && product.IndexedProduct.IsBuyable.GetValueOrDefault(false)
                && CheckPricePolicy(product);
        }

        /// <summary>
        /// Represents a price policy for a product. By default, product price should be greater than zero.
        /// </summary>
        /// <param name="product"></param>
        /// <returns></returns>
        protected virtual bool CheckPricePolicy(ExpProduct product)
        {
            return (product.AllPrices.FirstOrDefault()?.ListPrice.Amount ?? 0) > 0;
        }
    }
}
