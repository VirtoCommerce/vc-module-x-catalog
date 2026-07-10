using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Moq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.Xapi.Tests.Helpers;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Queries;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Queries
{
    public class SearchCategoryQueryHandlerTests : BaseMoqHelper
    {
        private const string CATALOG_ID = "catalog-1";

        private readonly Mock<ISearchProvider> _searchProviderMock = new();
        private readonly Mock<IMapper> _mapperMock = new();
        private readonly Mock<ISearchPhraseParser> _phraseParserMock = new();
        private readonly Mock<IStoreService> _storeServiceMock = new();
        private readonly Mock<IGenericPipelineLauncher> _pipelineMock = new();
        private readonly Mock<IPropertyService> _propertyServiceMock = new();

        public SearchCategoryQueryHandlerTests()
        {
            _propertyServiceMock
                .Setup(x => x.GetAllCatalogPropertiesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<Property>());

            _searchProviderMock
                .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchRequest>()))
                .ReturnsAsync(() => new SearchResponse
                {
                    TotalCount = 1,
                    Documents = new List<SearchDocument> { new SearchDocument { Id = "doc-1" } },
                });

            _mapperMock
                .Setup(x => x.Map<ExpCategory>(It.IsAny<object>(), It.IsAny<Action<IMappingOperationOptions<object, ExpCategory>>>()))
                .Returns(() => new ExpCategory { Category = new Category { Id = "category-1" } });
        }

        private SearchCategoryQueryHandler GetHandler() => new SearchCategoryQueryHandler(
            _searchProviderMock.Object,
            _mapperMock.Object,
            _phraseParserMock.Object,
            _storeServiceMock.Object,
            _pipelineMock.Object,
            _mediatorMock.Object,
            _propertyServiceMock.Object);

        [Fact]
        public async Task Handle_ObsoleteConstructorWithoutPropertyService_StillCompletesSearch()
        {
            // Back-compat: callers built against the pre-VC0016 constructor (no IPropertyService) must still
            // run a full search (just without multilanguage-aware filtering), not throw at Handle() time.
#pragma warning disable VC0016 // Type or member is obsolete
            var handler = new SearchCategoryQueryHandler(
                _searchProviderMock.Object,
                _mapperMock.Object,
                _phraseParserMock.Object,
                _storeServiceMock.Object,
                _pipelineMock.Object,
                _mediatorMock.Object);
#pragma warning restore VC0016

            var query = new SearchCategoryQuery
            {
                Store = new Store { Id = DEFAULT_STORE_ID, Catalog = CATALOG_ID },
                StoreId = DEFAULT_STORE_ID,
                CultureName = CULTURE_NAME,
                CurrencyCode = CURRENCY_CODE,
            };

            var result = await handler.Handle(query, CancellationToken.None);

            result.TotalCount.Should().Be(1);
            _propertyServiceMock.Verify(x => x.GetAllCatalogPropertiesAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
