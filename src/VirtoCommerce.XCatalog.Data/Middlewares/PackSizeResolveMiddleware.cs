using System;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    public class PackSizeResolveMiddleware : IAsyncMiddleware<SearchProductResponse>
    {
        public Task Run(SearchProductResponse parameter, Func<SearchProductResponse, Task> next)
        {
            var productsWithPackSize = parameter.Results.Where(expProduct => expProduct.IndexedProduct.PackSize > 1).Select(x => x.IndexedProduct);

            foreach (var product in productsWithPackSize)
            {
                var minQuantity = Math.Max(product.MinQuantity ?? 1, product.PackSize);
                // Ensure minQuantity is a multiple of PackSize
                if (minQuantity % product.PackSize > 0)
                {
                    minQuantity = (minQuantity / product.PackSize + 1) * product.PackSize;
                }
                product.MinQuantity = minQuantity;
            }

            return next(parameter);
        }
    }
}
