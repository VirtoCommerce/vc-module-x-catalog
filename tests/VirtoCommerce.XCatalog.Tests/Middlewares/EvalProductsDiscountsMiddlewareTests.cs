using System;
using FluentAssertions;
using Moq;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.MarketingModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Modules;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Services;
using VirtoCommerce.XCatalog.Data.Middlewares;
using VirtoCommerce.XCatalog.Tests.Helpers;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Middlewares
{
    public class EvalProductsDiscountsMiddlewareTests
    {
        [Fact]
        public void CreatePromoPriceMappingContext_ResponseCurrencySet_ContextCarriesResolvedCurrencyCode()
        {
            // Same rule as EvalProductsPricesMiddleware/SearchProductQueryHandler: the resolved currency
            // already on the carrier, not the raw request field, which may be unset.
            var currency = new Currency(CoreModule.Core.Common.Language.InvariantLanguage, "USD");
            var response = SearchProductResponseBuilder.Build(currency);
            response.Query.CurrencyCode = null;

            var middleware = CreateTestableMiddleware();

            var context = middleware.CallCreatePromoPriceMappingContext(response);

            context.CurrencyCode.Should().Be("USD");
        }

        private static TestableEvalProductsDiscountsMiddleware CreateTestableMiddleware()
        {
            var mapperMock = new Mock<IXCatalogMapper>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            var marketingEvaluatorOptionalDependency = new OptionalDependencyManager<IMarketingPromoEvaluator>(serviceProviderMock.Object);
            var pipelineMock = new Mock<IGenericPipelineLauncher>();

            return new TestableEvalProductsDiscountsMiddleware(mapperMock.Object, marketingEvaluatorOptionalDependency, pipelineMock.Object);
        }

        private sealed class TestableEvalProductsDiscountsMiddleware(
            IXCatalogMapper mapper,
            IOptionalDependency<IMarketingPromoEvaluator> marketingEvaluator,
            IGenericPipelineLauncher pipeline)
            : EvalProductsDiscountsMiddleware(mapper, marketingEvaluator, pipeline)
        {
            public PromoPriceMappingContext CallCreatePromoPriceMappingContext(SearchProductResponse response) =>
                CreatePromoPriceMappingContext(response);
        }
    }
}
