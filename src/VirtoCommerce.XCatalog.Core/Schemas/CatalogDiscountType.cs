using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Types;
using MediatR;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class CatalogDiscountType : DiscountType
    {
        public CatalogDiscountType(IDataLoaderContextAccessor dataLoader)
        {
            var promotion = new FieldType
            {
                Name = "promotion",
                Type = GraphTypeExtensionHelper.GetActualType<PromotionType>(),
                Arguments = new QueryArguments(),
                Resolver = new FuncFieldResolver<Discount, IDataLoaderResult<Promotion>>(async context =>
                {
                    var loader = dataLoader.Context.GetOrAddBatchLoader<string, Promotion>("promotionsLoader", ids => LoadPromotionsAsync(ids, context));
                    var result = loader.LoadAsync(context.Source.PromotionId);

                    return await Task.FromResult(result);
                })
            };

            AddField(promotion);
        }

        [Obsolete("Use the constructor without IMediator. The mediator is resolved from context.RequestServices per request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
        public CatalogDiscountType(IMediator mediator, IDataLoaderContextAccessor dataLoader)
            : this(dataLoader)
        {
        }

        protected virtual async Task<IDictionary<string, Promotion>> LoadPromotionsAsync(IEnumerable<string> ids, IResolveFieldContext context)
        {
            var result = await context.GetMediator().Send(new LoadPromotionsQuery { Ids = ids });

            return result.Promotions;
        }
    }
}
