using GraphQL.DI;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.InventoryModule.Core.Model.Search;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Data.Index;
using VirtoCommerce.XCatalog.Data.Middlewares;

namespace VirtoCommerce.XCatalog.Data.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddXCatalog(this IServiceCollection services, IGraphQLBuilder graphQLBuilder)
        {
            services.AddSingleton<ScopedSchemaFactory<DataAssemblyMarker>>();

            // The generic pipeline that is used for on-the-fly additional data evaluation (prices, inventories, discounts and taxes) for resulting products
            services.AddPipeline<SearchProductResponse>(builder =>
            {
                builder.AddMiddleware(typeof(EnsureCatalogProductLoadedMiddleware));
                builder.AddMiddleware(typeof(RemoveNullCatalogProductsMiddleware));
                builder.AddMiddleware(typeof(PackSizeResolveMiddleware));
                builder.AddMiddleware(typeof(EvalProductsPricesMiddleware));
                builder.AddMiddleware(typeof(EvalProductsDiscountsMiddleware));
                builder.AddMiddleware(typeof(EvalProductsTaxMiddleware));
                builder.AddMiddleware(typeof(EvalProductsInventoryMiddleware));
                builder.AddMiddleware(typeof(EvalProductsVendorMiddleware));
                builder.AddMiddleware(typeof(EnsurePropertyMetadataLoadedMiddleware));
                builder.AddMiddleware(typeof(ResolveSearchFiltersResponseMiddleware));
            });

            services.AddPipeline<SearchCategoryResponse>(builder =>
            {
                builder.AddMiddleware(typeof(EnsureCategoryLoadedMiddleware));
            });

            services.AddPipeline<IndexSearchRequestBuilder>(builder =>
            {
                builder.AddMiddleware(typeof(EvalSearchRequestUserGroupsMiddleware));
            });

            services.AddPipeline<InventorySearchCriteria>();

            services.AddPipeline<PriceEvaluationContext>();
            services.AddPipeline<PromotionEvaluationContext>();
            services.AddPipeline<TaxEvaluationContext>();

            return services;
        }
    }
}
