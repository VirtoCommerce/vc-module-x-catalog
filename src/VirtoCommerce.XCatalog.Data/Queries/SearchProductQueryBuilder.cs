using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using GraphQL.Types.Relay;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas;
using static VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class SearchProductQueryBuilder : CatalogQueryBuilder<SearchProductQuery, SearchProductResponse, ProductType>
    {
        protected override string Name => "products";

        protected virtual int DefaultPageSize => Connections.DefaultPageSize;

        public SearchProductQueryBuilder(
            IMediator mediator,
            IAuthorizationService authorizationService,
            IStoreService storeService,
            ICurrencyService currencyService)
            : base(mediator, authorizationService, storeService, currencyService)
        {
        }

        protected override FieldType GetFieldType()
        {
            var builder = GraphTypeExtensionHelper.CreateConnection<ProductType, EdgeType<ProductType>, ProductsConnectionType<ProductType>, object>(Name)
                .PageSize(DefaultPageSize);

            ConfigureArguments(builder.FieldType);

            builder.ResolveAsync(async context =>
            {
                var (query, response) = await Resolve(context);
                return new ProductsConnection<ExpProduct>(response.Results, query.Skip, query.Take, response.TotalCount)
                {
                    Facets = response.Facets,
                    Filters = response.Filters,
                };
            });

            return builder.FieldType;
        }

        protected override Task AfterMediatorSend(IResolveFieldContext<object> context, SearchProductQuery request, SearchProductResponse response)
        {
            var currencyCode = context.GetArgumentOrValue<string>("currencyCode");
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                context.SetCurrency(response.Currency);
            }

            return base.AfterMediatorSend(context, request, response);
        }
    }
}
