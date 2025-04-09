using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Moq;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Modules;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model.Search;
using VirtoCommerce.TaxModule.Core.Services;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Middlewares;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Middlewares
{
    public class EvalProductsTaxMiddlewareTest
    {
        [Fact]
        public void EvalProductsTaxMiddleware_TaxNotCalculatedWithoutResponseGroup_Success()
        {
            // Arrange
            var mapperMock = new Mock<IMapper>();
            var taxProviderSearchServiceMock = new Mock<IOptionalDependency<ITaxProviderSearchService>>();
            var genericPipelineLauncherMock = new Mock<IGenericPipelineLauncher>();

            var evalProductsTaxMiddleware = new EvalProductsTaxMiddleware(mapperMock.Object, taxProviderSearchServiceMock.Object, genericPipelineLauncherMock.Object);

            var response = new SearchProductResponse()
            {
                TotalCount = 1,
                Results = new List<ExpProduct>() { new ExpProduct() },
                Query = new SearchProductQuery() { CurrencyCode = "USD" }
            };

            //Act
            evalProductsTaxMiddleware.Run(response, resp => Task.CompletedTask);

            // Assert
            taxProviderSearchServiceMock
                .Verify(x => x.Value.SearchAsync(It.IsAny<TaxProviderSearchCriteria>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public void EvalProductsTaxMiddleware_TaxNotCalculatedWithoutTaxProvider_Success()
        {
            // Arrange
            var mapperMock = new Mock<IMapper>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            var taxProviderSearchServiceMock = new Mock<ITaxProviderSearchService>();
            var taxProviderSearchServiceOptionalDependency = new OptionalDependencyManager<ITaxProviderSearchService>(serviceProviderMock.Object);

            serviceProviderMock.Setup(x => x.GetService(typeof(ITaxProviderSearchService))).Returns(taxProviderSearchServiceMock.Object);
            taxProviderSearchServiceMock
                .Setup(x => x.SearchAsync(It.IsAny<TaxProviderSearchCriteria>(), It.IsAny<bool>()))
                .ReturnsAsync(() => new TaxProviderSearchResult()
                {
                    TotalCount = 0,
                    Results = new List<TaxProvider>()
                });
            var genericPipelineLauncherMock = new Mock<IGenericPipelineLauncher>();

            var evalProductsTaxMiddleware = new EvalProductsTaxMiddleware(mapperMock.Object, taxProviderSearchServiceOptionalDependency, genericPipelineLauncherMock.Object);

            var response = new SearchProductResponse()
            {
                TotalCount = 1,
                Results = new List<ExpProduct>() { new ExpProduct() },
                Query = new SearchProductQuery()
                {
                    CurrencyCode = "USD",
                    IncludeFields = new List<string>() { "price" }  //ResponseGroup.LoadPrices
                },
                Store = GetStore(),
            };

            //Act
            Action action = () => evalProductsTaxMiddleware.Run(response, resp => Task.CompletedTask);

            // Assert
            action.Should().NotThrow();
            taxProviderSearchServiceMock
                .Verify(x => x.SearchAsync(It.IsAny<TaxProviderSearchCriteria>(), It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public void EvalProductsTaxMiddleware_TaxRatesCalculated_Success()
        {
            // Arrange
            var taxProviderMock = new Mock<TaxProvider>();
            taxProviderMock.Object.IsActive = true;

            var mapperMock = new Mock<IMapper>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            var taxProviderSearchServiceMock = new Mock<ITaxProviderSearchService>();
            var taxProviderSearchServiceOptionalDependency = new OptionalDependencyManager<ITaxProviderSearchService>(serviceProviderMock.Object);

            serviceProviderMock.Setup(x => x.GetService(typeof(ITaxProviderSearchService))).Returns(taxProviderSearchServiceMock.Object);
            taxProviderSearchServiceMock
                .Setup(x => x.SearchAsync(It.IsAny<TaxProviderSearchCriteria>(), It.IsAny<bool>()))
                .ReturnsAsync(() => new TaxProviderSearchResult()
                {
                    TotalCount = 1,
                    Results = new List<TaxProvider>() { taxProviderMock.Object }
                });
            var genericPipelineLauncherMock = new Mock<IGenericPipelineLauncher>();

            var evalProductsTaxMiddleware = new EvalProductsTaxMiddleware(mapperMock.Object, taxProviderSearchServiceOptionalDependency, genericPipelineLauncherMock.Object);

            var response = new SearchProductResponse()
            {
                TotalCount = 1,
                Results = new List<ExpProduct>()
                {
                    new ExpProduct()
                },
                Query = new SearchProductQuery()
                {
                    CurrencyCode = "USD",
                    IncludeFields = new List<string>() { "price" }  //ResponseGroup.LoadPrices
                },
                Store = GetStore(),
            };

            //Act
            Action action = () => evalProductsTaxMiddleware.Run(response, resp => Task.CompletedTask);

            // Assert
            action.Should().NotThrow();
            taxProviderSearchServiceMock
                .Verify(x => x.SearchAsync(It.IsAny<TaxProviderSearchCriteria>(), It.IsAny<bool>()), Times.Once);
            taxProviderMock.Verify(x => x.CalculateRates(It.IsAny<TaxEvaluationContext>()), Times.Once);
        }

        [Fact]
        public void EvalProductsTaxMiddleware_TaxesApply_Success()
        {
            // Arrange
            var currency = new Currency(Language.InvariantLanguage, "USD")
            {
                RoundingPolicy = new DefaultMoneyRoundingPolicy()
            };

            var productPrice = new Mock<ProductPrice>(currency).Object;
            productPrice.TaxPercentRate = 0;
            productPrice.ListPrice = new Money(100m, currency);
            productPrice.DiscountAmount = new Money(0m, currency);
            productPrice.ProductId = "someId";

            var taxProviderMock = new Mock<TaxProvider>();
            taxProviderMock.Object.IsActive = true;

            taxProviderMock.Setup(x => x.CalculateRates(It.IsAny<TaxEvaluationContext>()))
                .Returns(() => new List<TaxRate>() { new TaxRate() { Currency = "USD", Rate = 50, Line = new TaxLine() { Id = "someId", Quantity = 0 } } });

            var mapperMock = new Mock<IMapper>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            var taxProviderSearchServiceMock = new Mock<ITaxProviderSearchService>();
            var taxProviderSearchServiceOptionalDependency = new OptionalDependencyManager<ITaxProviderSearchService>(serviceProviderMock.Object);

            serviceProviderMock.Setup(x => x.GetService(typeof(ITaxProviderSearchService))).Returns(taxProviderSearchServiceMock.Object);
            taxProviderSearchServiceMock
                .Setup(x => x.SearchAsync(It.IsAny<TaxProviderSearchCriteria>(), It.IsAny<bool>()))
                .ReturnsAsync(() => new TaxProviderSearchResult()
                {
                    TotalCount = 0,
                    Results = new List<TaxProvider>() { taxProviderMock.Object }
                });
            var genericPipelineLauncherMock = new Mock<IGenericPipelineLauncher>();

            var evalProductsTaxMiddleware = new EvalProductsTaxMiddleware(mapperMock.Object, taxProviderSearchServiceOptionalDependency, genericPipelineLauncherMock.Object);

            var response = new SearchProductResponse()
            {
                TotalCount = 1,
                Results = new List<ExpProduct>()  {
                    new ExpProduct()
                    {

                        AllPrices = new List<ProductPrice>()
                        {
                            productPrice
                        }

                    }
                },
                Query = new SearchProductQuery() { CurrencyCode = "USD", IncludeFields = new List<string>() { "price" } },
                Store = GetStore(),
            };

            //Act
            Action action = () => evalProductsTaxMiddleware.Run(response, resp => Task.CompletedTask);

            // Assert
            action.Should().NotThrow();
            taxProviderSearchServiceMock
                .Verify(x => x.SearchAsync(It.IsAny<TaxProviderSearchCriteria>(), It.IsAny<bool>()), Times.Once);
            taxProviderMock.Verify(x => x.CalculateRates(It.IsAny<TaxEvaluationContext>()), Times.Once);
            productPrice.TaxPercentRate.Should().Be(0.5m);
        }

        private static StoreModule.Core.Model.Store GetStore()
        {
            return new StoreModule.Core.Model.Store
            {
                Settings = new List<ObjectSettingEntry>()
                {
                    new ObjectSettingEntry
                    {
                        Name = StoreModule.Core.ModuleConstants.Settings.General.TaxCalculationEnabled.Name,
                        Value = true,
                    }
                }
            };
        }
    }
}
