using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.InventoryModule.Core.Model.Search;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class SearchFulfillmentCentersQueryHandler : IRequestHandler<SearchFulfillmentCentersQuery, FulfillmentCenterSearchResult>
    {
        private readonly IFulfillmentCenterSearchService _fulfillmentCenterSearchService;
        private readonly IStoreService _storeService;

        public SearchFulfillmentCentersQueryHandler(
            IOptionalDependency<IFulfillmentCenterSearchService> fulfillmentCenterSearchService,
            IStoreService storeService)
        {
            _fulfillmentCenterSearchService = fulfillmentCenterSearchService.Value;
            _storeService = storeService;
        }

        public async Task<FulfillmentCenterSearchResult> Handle(SearchFulfillmentCentersQuery request, CancellationToken cancellationToken)
        {
            if (_fulfillmentCenterSearchService == null)
            {
                return new FulfillmentCenterSearchResult();
            }

            if (!string.IsNullOrEmpty(request.StoreId))
            {
                var store = await _storeService.GetNoCloneAsync(request.StoreId);
                if (store != null)
                {
                    var fulfillmentCenterIds = new List<string>
                    {
                        store.MainFulfillmentCenterId,
                        store.MainReturnsFulfillmentCenterId,
                    };

                    fulfillmentCenterIds.AddRange(store.AdditionalFulfillmentCenterIds);
                    fulfillmentCenterIds.AddRange(store.ReturnsFulfillmentCenterIds);

                    request.FulfillmentCenterIds = fulfillmentCenterIds.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
                }
            }

            var searchCriteria = new FulfillmentCenterSearchCriteria
            {
                Skip = request.Skip,
                Take = request.Take,
                Sort = request.Sort,
                Keyword = request.Query,
                ObjectIds = request.FulfillmentCenterIds,
            };

            return await _fulfillmentCenterSearchService.SearchAsync(searchCriteria);
        }
    }
}
