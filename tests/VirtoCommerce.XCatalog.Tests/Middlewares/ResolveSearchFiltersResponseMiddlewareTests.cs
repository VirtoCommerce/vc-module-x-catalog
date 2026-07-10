using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.SearchModule.Core.Extensions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Middlewares;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Middlewares
{
    public class ResolveSearchFiltersResponseMiddlewareTests
    {
        [Fact]
        public async Task Run_CultureAwareOrFilter_ReportedUnderBaseFieldName()
        {
            // Arrange: mirrors ConvertFilter() wrapping a culture-aware term filter into an OrFilter
            // of the base field and the "{field}_{culture}" field (same Values in both).
            var orFilter = new TermFilter { FieldName = "MLFILTER_TEST", Values = ["AlphaGerman"] }
                .Or(new TermFilter { FieldName = "mlfilter_test_de-de", Values = ["AlphaGerman"] });

            var propertyServiceMock = new Mock<IPropertyService>();
            propertyServiceMock
                .Setup(x => x.GetAllCatalogPropertiesAsync(It.IsAny<string>()))
                .ReturnsAsync([]);

            var dictionaryItemSearchServiceMock = new Mock<IPropertyDictionaryItemSearchService>();
            var categoryServiceMock = new Mock<ICategoryService>();

            var middleware = new ResolveSearchFiltersResponseMiddleware(
                propertyServiceMock.Object,
                dictionaryItemSearchServiceMock.Object,
                categoryServiceMock.Object);

            var response = new SearchProductResponse
            {
                Query = new SearchProductQuery { IncludeFields = ["filters"] },
                UserFilters = [orFilter],
                GeneratedFilters = [],
                Results = [],
                Store = new Store { DefaultLanguage = "en-US" },
            };

            // Act
            await middleware.Run(response, _ => Task.CompletedTask);

            // Assert
            response.Filters.Should().ContainSingle();

            var filter = response.Filters.Single();

            filter.Name.Should().Be("MLFILTER_TEST");
            filter.FilterType.Should().Be("term");
            filter.TermValues.Select(x => x.Value).Should().BeEquivalentTo(["AlphaGerman"]);
        }
    }
}
