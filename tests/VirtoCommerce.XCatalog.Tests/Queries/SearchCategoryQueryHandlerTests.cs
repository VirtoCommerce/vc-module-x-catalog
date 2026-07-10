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
        private const string SCORE_FIELD = "score";

        private readonly Mock<ISearchProvider> _searchProviderMock = new();
        private readonly Mock<IMapper> _mapperMock = new();
        private readonly Mock<ISearchPhraseParser> _phraseParserMock = new();
        private readonly Mock<IStoreService> _storeServiceMock = new();
        private readonly Mock<IGenericPipelineLauncher> _pipelineMock = new();
        private readonly Mock<IPropertyService> _propertyServiceMock = new();

        private readonly List<SearchRequest> _capturedSearchRequests = new();

        public SearchCategoryQueryHandlerTests()
        {
            _propertyServiceMock
                .Setup(x => x.GetAllCatalogPropertiesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<Property>());

            // Capture every SearchRequest reaching the search provider (top-level + each recursive child fetch).
            _searchProviderMock
                .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchRequest>()))
                .Callback<string, SearchRequest>((_, request) => _capturedSearchRequests.Add(request))
                .ReturnsAsync(() => new SearchResponse
                {
                    TotalCount = 1,
                    Documents = new List<SearchDocument> { new SearchDocument { Id = "doc-1" } },
                });

            // Each mapped category is a fresh instance with a null ChildCategories so the child-loading branch is entered.
            _mapperMock
                .Setup(x => x.Map<ExpCategory>(It.IsAny<object>(), It.IsAny<Action<IMappingOperationOptions<object, ExpCategory>>>()))
                .Returns(() => new ExpCategory { Category = new Category { Id = "category-1" } });

            // The child-categories lookup returns one child id so the recursive search is triggered.
            _mediatorMock
                .Setup(x => x.Send(It.IsAny<ChildCategoriesQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChildCategoriesQueryResponse
                {
                    ChildCategories = new List<ExpCategory> { new ExpCategory { Key = "child-1" } },
                });
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
        public async Task Handle_SearchCategory_WithSortAndChildCategories_PropagatesSortToChildSearch()
        {
            // Arrange
            var query = new SearchCategoryQuery
            {
                Store = new Store { Id = DEFAULT_STORE_ID, Catalog = CATALOG_ID },
                StoreId = DEFAULT_STORE_ID,
                CultureName = CULTURE_NAME,
                CurrencyCode = CURRENCY_CODE,
                Sort = "priority;name",
                IncludeFields = new List<string> { "childCategories.id" },
            };

            // Act
            await GetHandler().Handle(query, CancellationToken.None);

            // Assert
            // Two searches must have run: the top-level one and the recursive child fetch.
            _capturedSearchRequests.Should().HaveCount(2);

            var childSearchRequest = _capturedSearchRequests[1];

            // The child search must honor the requested sort (priority/name) rather than fall back to the default [score desc].
            childSearchRequest.Sorting.Should().Contain(x => x.FieldName == "priority");
            childSearchRequest.Sorting.Should().Contain(x => x.FieldName == "name");
            childSearchRequest.Sorting.Should().NotContain(x => x.FieldName == SCORE_FIELD);
        }

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
