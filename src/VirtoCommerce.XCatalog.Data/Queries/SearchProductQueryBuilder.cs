using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using GraphQL.Types.Relay;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Schemas;
using VirtoCommerce.XDigitalCatalog.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class SearchProductQueryBuilder : CatalogQueryBuilder<SearchProductQuery, SearchProductResponse, ProductType>
    {
        protected override string Name => "products";

        protected virtual int DefaultPageSize => 20;

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
            var builder = GraphTypeExtenstionHelper.CreateConnection<ProductType, EdgeType<ProductType>, ProductsConnectonType<ProductType>, object>()
                .Name(Name)
                .PageSize(DefaultPageSize);

            ConfigureArguments(builder.FieldType);

            builder.ResolveAsync(async context =>
            {
                var (query, response) = await Resolve(context);
                return new ProductsConnection<ExpProduct>(response.Results, query.Skip, query.Take, response.TotalCount)
                {
                    Facets = response.Facets,
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
