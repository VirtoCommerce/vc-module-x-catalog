using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.MarketingModule.Core.Model.Promotions.Search;
using VirtoCommerce.MarketingModule.Core.Search;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class LoadPromotionsQueryHandler : IQueryHandler<LoadPromotionsQuery, LoadPromotionsResponse>
    {
        private readonly IPromotionSearchService _promotionSearchService;

        public LoadPromotionsQueryHandler(IPromotionSearchService promotionSearchService)
        {
            _promotionSearchService = promotionSearchService;
        }

        public virtual async Task<LoadPromotionsResponse> Handle(LoadPromotionsQuery request, CancellationToken cancellationToken)
        {
            var promotions = await _promotionSearchService.SearchPromotionsAsync(new PromotionSearchCriteria
            {
                ObjectIds = request.Ids.ToArray(),
            });

            return new LoadPromotionsResponse
            {
                Promotions = promotions.Results.ToDictionary(x => x.Id)
            };
        }
    }
}
