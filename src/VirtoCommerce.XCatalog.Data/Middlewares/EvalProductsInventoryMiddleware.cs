using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.InventoryModule.Core.Model.Search;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    public class EvalProductsInventoryMiddleware : IAsyncMiddleware<SearchProductResponse>
    {
        private readonly IInventorySearchService _inventorySearchService;
        private readonly IGenericPipelineLauncher _pipeline;

        public EvalProductsInventoryMiddleware(IInventorySearchService inventorySearchService, IGenericPipelineLauncher pipeline)
        {
            _inventorySearchService = inventorySearchService;
            _pipeline = pipeline;
        }

        public virtual async Task Run(SearchProductResponse parameter, Func<SearchProductResponse, Task> next)
        {
            ArgumentNullException.ThrowIfNull(parameter);

            var query = parameter.Query;
            if (query == null)
            {
                throw new OperationCanceledException("Query must be set");
            }

            var productIds = parameter.Results.Select(x => x.Id).ToArray();
            var responseGroup = EnumUtility.SafeParse(query.GetResponseGroup(), ExpProductResponseGroup.None);

            // If products availabilities requested
            if (responseGroup.HasFlag(ExpProductResponseGroup.LoadInventories) &&
                productIds.Length != 0)
            {
                var inventories = new List<InventoryInfo>();

                var skip = 0;
                var take = 50;
                InventoryInfoSearchResult searchResult;

                var searchCriteria = await GetInventorySearchCriteria(productIds);

                do
                {
                    searchCriteria.Take = take;
                    searchCriteria.Skip = skip;

                    searchResult = await _inventorySearchService.SearchInventoriesAsync(searchCriteria);

                    inventories.AddRange(searchResult.Results);
                    skip += take;
                }
                while (searchResult.Results.Count == take);

                if (inventories.Count != 0)
                {
                    parameter.Results.Apply(x => x.ApplyStoreInventories(inventories, parameter.Store));
                }
            }

            await next(parameter);
        }

        protected virtual async Task<InventorySearchCriteria> GetInventorySearchCriteria(IList<string> productIds)
        {
            var searchCriteria = AbstractTypeFactory<InventorySearchCriteria>.TryCreateInstance();

            searchCriteria.ProductIds = productIds;

            await _pipeline.Execute(searchCriteria);

            return searchCriteria;
        }
    }
}
