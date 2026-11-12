using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Modules;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.PricingModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Services;
using VirtoCommerce.XCatalog.Data.Middlewares;
using VirtoCommerce.XCatalog.Tests.Helpers;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Middlewares
{
    public class EvalProductsPricesMiddlewareTests
    {
        [Fact]
        public void CreateProductPricesMappingContext_ResponseCurrencySet_ContextCarriesResolvedCurrencyCode()
        {
            // response.Query.CurrencyCode is the raw, unnormalized request value (null when the client
            // omits it); response.Currency is the store-resolved currency, already on the carrier by the
            // time this hook runs. Same rule as SearchProductQueryHandler.CreateFacetMappingContext.
            var currency = new Currency(CoreModule.Core.Common.Language.InvariantLanguage, "USD");
            var response = SearchProductResponseBuilder.Build(currency);
            response.Query.CurrencyCode = null;

            var middleware = CreateTestableMiddleware();

            var context = middleware.CallCreateProductPricesMappingContext(response, []);

            context.CurrencyCode.Should().Be("USD");
        }

        private static TestableEvalProductsPricesMiddleware CreateTestableMiddleware()
        {
            var mapperMock = new Mock<IXCatalogMapper>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            var pricingEvaluatorServiceOptionalDependency = new OptionalDependencyManager<IPricingEvaluatorService>(serviceProviderMock.Object);
            var pipelineMock = new Mock<IGenericPipelineLauncher>();
            var storeServiceMock = new Mock<IStoreService>();

            return new TestableEvalProductsPricesMiddleware(mapperMock.Object, pricingEvaluatorServiceOptionalDependency, pipelineMock.Object, storeServiceMock.Object);
        }

        private sealed class TestableEvalProductsPricesMiddleware(
            IXCatalogMapper mapper,
            IOptionalDependency<IPricingEvaluatorService> pricingEvaluatorService,
            IGenericPipelineLauncher pipeline,
            IStoreService storeService)
            : EvalProductsPricesMiddleware(mapper, pricingEvaluatorService, pipeline, storeService)
        {
            public ProductPricesMappingContext CallCreateProductPricesMappingContext(SearchProductResponse response, IEnumerable<Pricelist> pricelists) =>
                CreateProductPricesMappingContext(response, pricelists);
        }
    }
}
