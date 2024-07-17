using System;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    public class EnsureCategoryLoadedMiddleware : IAsyncMiddleware<SearchCategoryResponse>
    {
        private readonly ICategoryService _categoryService;

        public EnsureCategoryLoadedMiddleware(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task Run(SearchCategoryResponse parameter, Func<SearchCategoryResponse, Task> next)
        {
            var itemsIds = parameter.Results
                .Where(expProduct => expProduct.Category is null)
                .Select(expProduct => expProduct.Key)
                .Where(key => key != null)
                .ToArray();

            if (itemsIds.Length != 0)
            {
                var responseGroup = parameter.Query.GetCategoryResponseGroup();
                var categories = await _categoryService.GetAsync(itemsIds, responseGroup);

                foreach (var category in categories)
                {
                    var item = parameter.Results.FirstOrDefault(expProduct => expProduct.Key == category.Id);
                    if (item is null)
                    {
                        continue;
                    }

                    item.Category ??= category;
                }
            }

            await next(parameter);
        }
    }
}
