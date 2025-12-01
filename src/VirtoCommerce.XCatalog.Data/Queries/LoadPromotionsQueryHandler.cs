using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.MarketingModule.Core.Model.Promotions.Search;
using VirtoCommerce.MarketingModule.Core.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class LoadPromotionsQueryHandler : IQueryHandler<LoadPromotionsQuery, LoadPromotionsResponse>
    {
        private readonly IPromotionSearchService _promotionSearchService;

        public LoadPromotionsQueryHandler(IOptionalDependency<IPromotionSearchService> promotionSearchService)
        {
            _promotionSearchService = promotionSearchService.Value;
        }

        public virtual async Task<LoadPromotionsResponse> Handle(LoadPromotionsQuery request, CancellationToken cancellationToken)
        {
            if (_promotionSearchService == null)
            {
                return new LoadPromotionsResponse();
            }

            var searchCriteria = AbstractTypeFactory<PromotionSearchCriteria>.TryCreateInstance();
            searchCriteria.ObjectIds = request.Ids.ToArray();

            var searchResult = await _promotionSearchService.SearchAsync(searchCriteria);

            return new LoadPromotionsResponse
            {
                Promotions = searchResult.Results.ToDictionary(x => x.Id)
            };
        }
    }
}
