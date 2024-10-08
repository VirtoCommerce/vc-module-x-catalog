using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Types;
using MediatR;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class CatalogDiscountType : DiscountType
    {
        public CatalogDiscountType(IMediator mediator, IDataLoaderContextAccessor dataLoader)
        {
            var promotion = new EventStreamFieldType
            {
                Name = "promotion",
                Type = GraphTypeExtenstionHelper.GetActualType<PromotionType>(),
                Arguments = new QueryArguments(),
                Resolver = new FuncFieldResolver<Discount, IDataLoaderResult<Promotion>>(context =>
                {
                    var loader = dataLoader.Context.GetOrAddBatchLoader<string, Promotion>("promotionsLoader", (ids) => LoadPromotionsAsync(mediator, ids));
                    return loader.LoadAsync(context.Source.PromotionId);
                })
            };
            AddField(promotion);
        }

        protected virtual async Task<IDictionary<string, Promotion>> LoadPromotionsAsync(IMediator mediator, IEnumerable<string> ids)
        {
            var result = await mediator.Send(new LoadPromotionsQuery { Ids = ids });

            return result.Promotions;
        }
    }
}
