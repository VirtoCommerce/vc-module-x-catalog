using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CatalogModule.Core.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class LoadRelatedSlugPathQueryHandler : IQueryHandler<LoadRelatedSlugPathQuery, LoadRelatedSlugPathResponse>
    {
        private readonly IStoreService _storeService;

        public LoadRelatedSlugPathQueryHandler(IStoreService storeService)
        {
            _storeService = storeService;
        }

        public virtual async Task<LoadRelatedSlugPathResponse> Handle(LoadRelatedSlugPathQuery request, CancellationToken cancellationToken)
        {
            var store = await _storeService.GetByIdAsync(request.StoreId);
            if (store is null)
            {
                return new LoadRelatedSlugPathResponse();
            }

            var language = request.CultureName ?? store.DefaultLanguage;
            var slug = request.Outlines.GetSeoPath(store, language);

            return new LoadRelatedSlugPathResponse
            {
                Slug = slug
            };
        }
    }
}
