using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class LoadRelatedCatalogOutlineQueryHandler : IQueryHandler<LoadRelatedCatalogOutlineQuery, LoadRelatedCatalogOutlineResponse>
    {
        private readonly IStoreService _storeService;

        public LoadRelatedCatalogOutlineQueryHandler(IStoreService storeService)
        {
            _storeService = storeService;
        }

        public virtual async Task<LoadRelatedCatalogOutlineResponse> Handle(LoadRelatedCatalogOutlineQuery request, CancellationToken cancellationToken)
        {
            var store = await _storeService.GetNoCloneAsync(request.StoreId);
            if (store is null)
            {
                return new LoadRelatedCatalogOutlineResponse();
            }

            return new LoadRelatedCatalogOutlineResponse
            {
                Outline = request.Outlines.GetBestOutlinePath(store.Catalog, request.PreviousOutline),
            };
        }
    }
}
