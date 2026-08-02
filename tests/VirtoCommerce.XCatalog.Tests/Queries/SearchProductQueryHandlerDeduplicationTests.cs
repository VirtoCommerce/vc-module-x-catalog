using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Search.Sorting;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
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
using Aggregation = VirtoCommerce.CatalogModule.Core.Model.Search.Aggregation;
using CatalogProductSorting = VirtoCommerce.CatalogModule.Core.Search.Sorting.ProductSorting;

namespace VirtoCommerce.XCatalog.Tests.Queries
{
    /// <summary>
    /// End-to-end tests for the request-scoped search deduplication. These drive <c>Handle</c> rather than a
    /// projection helper, which is why they need the fixture below - the pre-existing product-handler tests
    /// only exercise <c>BuildSortings</c> and never reach the search provider.
    /// </summary>
    public class SearchProductQueryHandlerDeduplicationTests : BaseMoqHelper
    {
        private const string CATALOG_ID = "catalog-1";
        private const string START_DATE_FIELD = "startdate";

        private readonly Mock<ISearchProvider> _searchProviderMock = new();
        private readonly Mock<IMapper> _mapperMock = new();
        private readonly Mock<IStoreCurrencyResolver> _storeCurrencyResolverMock = new();
        private readonly Mock<IStoreService> _storeServiceMock = new();
        private readonly Mock<IGenericPipelineLauncher> _pipelineMock = new();
        private readonly Mock<IAggregationConverter> _aggregationConverterMock = new();
        private readonly Mock<ISearchPhraseParser> _phraseParserMock = new();
        private readonly Mock<IProductSortingService> _productSortingServiceMock = new();
        private readonly Mock<IPropertyService> _propertyServiceMock = new();

        // Provider calls, NOT Handle entries: Handle(LoadProductsQuery) re-enters Handle(SearchProductQuery),
        // so counting handler entries double-counts a single logical search.
        private readonly List<SearchRequest> _capturedSearchRequests = [];

        public SearchProductQueryHandlerDeduplicationTests()
        {
            var currency = GetCurrency();

            _storeCurrencyResolverMock
                .Setup(x => x.GetAllStoreCurrenciesAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync([currency]);

            _storeCurrencyResolverMock
                .Setup(x => x.GetStoreCurrencyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(currency);

            // GetByIdAsync is an extension over ICrudService.GetAsync, so the mock has to target GetAsync.
            // Languages must be non-null: Handle reads store.Languages.Contains(...) before anything else.
            _storeServiceMock
                .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync([new Store
                {
                    Id = DEFAULT_STORE_ID,
                    Catalog = CATALOG_ID,
                    Languages = [CULTURE_NAME],
                    DefaultLanguage = CULTURE_NAME,
                    DefaultCurrency = CURRENCY_CODE,
                }]);

            _propertyServiceMock
                .Setup(x => x.GetAllCatalogPropertiesAsync(It.IsAny<string>()))
                .ReturnsAsync([]);

            _productSortingServiceMock
                .Setup(x => x.GetSortingsAsync(It.IsAny<ProductSortingContext>()))
                .ReturnsAsync(new List<CatalogProductSorting>());

            // Non-null aggregations: ConvertAggregations feeds ApplyFacetLocalization and SetAppliedAggregations.
            _aggregationConverterMock
                .Setup(x => x.ConvertAggregationsAsync(It.IsAny<IList<AggregationResponse>>(), It.IsAny<ProductIndexedSearchCriteria>()))
                .ReturnsAsync([]);

            _mapperMock
                .Setup(x => x.Map<ExpProduct>(It.IsAny<object>()))
                .Returns(() => new ExpProduct());

            SetupProviderResponse();
        }

        // Every instance the provider hands out is recorded, so a test can assert that NO caller ever holds
        // it. Comparing the two callers against each other is not enough: cloning only on the hit would
        // still make them differ, while leaving the first caller aliased to the cached instance.
        private readonly List<SearchResponse> _providerResponses = [];

        private void SetupProviderResponse()
        {
            _searchProviderMock
                .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchRequest>()))
                .Callback<string, SearchRequest>((_, request) => _capturedSearchRequests.Add(request))
                .ReturnsAsync(() =>
                {
                    var response = new SearchResponse
                    {
                        TotalCount = 1,
                        Documents = [new SearchDocument { Id = "doc-1" }],
                        Aggregations = [new AggregationResponse
                        {
                            Id = "brand",
                            Values = [new AggregationResponseValue { Id = "acme", Count = 3 }],
                        }],
                    };

                    _providerResponses.Add(response);

                    return response;
                });
        }

        private TestableHandler GetHandler(IRequestScopedCache cache = null)
        {
            var accessorMock = new Mock<IRequestScopedCacheAccessor>();
            accessorMock.Setup(x => x.Cache).Returns(cache);

            return new TestableHandler(
                _searchProviderMock.Object,
                _mapperMock.Object,
                _storeCurrencyResolverMock.Object,
                _storeServiceMock.Object,
                _pipelineMock.Object,
                _aggregationConverterMock.Object,
                _phraseParserMock.Object,
                _productSortingServiceMock.Object,
                _propertyServiceMock.Object,
                accessorMock.Object);
        }

        private static SearchProductQuery Query(string keyword = null) => new()
        {
            StoreId = DEFAULT_STORE_ID,
            CultureName = CULTURE_NAME,
            CurrencyCode = CURRENCY_CODE,
            Query = keyword,
            IncludeFields = ["id"],
        };

        [Fact]
        public async Task Handle_TwoDifferentSearchesInOneScope_ShareOneCertainDate()
        {
            // Two DIFFERENT searches, so both reach the provider and both certain-date filters are observable.
            // The converse is deliberately not asserted by comparing two DateTime.UtcNow reads for inequality:
            // they can land in the same tick and such a test would flake. B15 covers the no-scope case
            // behaviourally instead.
            var handler = GetHandler(new RequestScopedCache());

            await handler.Handle(Query("first"), CancellationToken.None);
            await handler.Handle(Query("second"), CancellationToken.None);

            _capturedSearchRequests.Should().HaveCount(2);

            var first = CertainDateOf(_capturedSearchRequests[0]);
            var second = CertainDateOf(_capturedSearchRequests[1]);

            first.Should().NotBeNull("the certain-date filter must be present for this test to mean anything");
            second.Should().Be(first);
        }

        [Fact]
        public async Task Handle_TwoIdenticalSearchesInOneScope_CallTheProviderOnce()
        {
            var handler = GetHandler(new RequestScopedCache());

            await handler.Handle(Query("shoes"), CancellationToken.None);
            await handler.Handle(Query("shoes"), CancellationToken.None);

            _capturedSearchRequests.Should().HaveCount(1);
        }

        [Fact]
        public async Task Handle_TwoDifferentSearchesInOneScope_CallTheProviderTwice()
        {
            var handler = GetHandler(new RequestScopedCache());

            await handler.Handle(Query("shoes"), CancellationToken.None);
            await handler.Handle(Query("boots"), CancellationToken.None);

            _capturedSearchRequests.Should().HaveCount(2);
        }

        [Fact]
        public async Task Handle_WithoutAmbientScope_CallsTheProviderPerSendAndReturnsItsOwnInstance()
        {
            // The pre-cache behaviour, which the null-accessor path must preserve exactly.
            var handler = GetHandler(cache: null);

            await handler.Handle(Query("shoes"), CancellationToken.None);
            await handler.Handle(Query("shoes"), CancellationToken.None);

            _capturedSearchRequests.Should().HaveCount(2);

            var response = new SearchResponse();
            _searchProviderMock
                .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchRequest>()))
                .ReturnsAsync(response);

            var returned = await handler.CallSearchProductsAsync(new SearchRequest());

            returned.Should().BeSameAs(response, "with no cache there is nothing to isolate callers from");
        }

        [Fact]
        public async Task SearchProductsAsync_CachedHitAndMiss_BothReturnACopy()
        {
            // The miss matters as much as the hit: the cache stores the task the factory returned, so the
            // caller that populated the entry holds the instance every later caller is handed.
            var handler = GetHandler(new RequestScopedCache());
            var request = new SearchRequest();

            var onMiss = await handler.CallSearchProductsAsync(request);
            var onHit = await handler.CallSearchProductsAsync(request);

            _capturedSearchRequests.Should().HaveCount(1);

            // The load-bearing assertion: the caller that POPULATED the entry must not hold the cached
            // instance either. Cloning only on the hit passes an onHit-vs-onMiss comparison while leaving
            // this one aliased.
            onMiss.Should().NotBeSameAs(_providerResponses[0]);
            onHit.Should().NotBeSameAs(_providerResponses[0]);

            onHit.Should().NotBeSameAs(onMiss);
            onHit.Aggregations[0].Should().NotBeSameAs(onMiss.Aggregations[0]);
            onHit.Aggregations[0].Values[0].Should().NotBeSameAs(onMiss.Aggregations[0].Values[0]);
        }

        [Fact]
        public async Task SearchProductsAsync_FaultedSearch_PropagatesAndIsNotRetried()
        {
            _searchProviderMock
                .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchRequest>()))
                .Callback<string, SearchRequest>((_, request) => _capturedSearchRequests.Add(request))
                .ThrowsAsync(new InvalidOperationException("index unavailable"));

            var handler = GetHandler(new RequestScopedCache());
            var request = new SearchRequest();

            await FluentActions.Awaiting(() => handler.CallSearchProductsAsync(request)).Should().ThrowAsync<InvalidOperationException>();
            await FluentActions.Awaiting(() => handler.CallSearchProductsAsync(request)).Should().ThrowAsync<InvalidOperationException>();

            _capturedSearchRequests.Should().HaveCount(1, "a faulted load stays cached for the rest of the request");
        }

        [Fact]
        public void CloneSearchResponse_TwoClones_ShareNoMutableNode()
        {
            var handler = GetHandler();
            var source = new SearchResponse
            {
                TotalCount = 7,
                Documents = [new SearchDocument { Id = "doc-1" }],
                Aggregations = [new AggregationResponse
                {
                    Id = "brand",
                    Values = [new AggregationResponseValue { Id = "acme", Count = 3 }],
                }],
            };

            var first = handler.CallCloneSearchResponse(source);
            var second = handler.CallCloneSearchResponse(source);

            first.Should().NotBeSameAs(second);
            first.Aggregations[0].Should().NotBeSameAs(second.Aggregations[0]);
            first.Aggregations[0].Values[0].Should().NotBeSameAs(second.Aggregations[0].Values[0]);

            // The aggregation converter rewrites these ids in place; one caller's rewrite must not be visible
            // to another. This is the assertion a shallow copy of the value list fails.
            first.Aggregations[0].Id = "rewritten";
            first.Aggregations[0].Values[0].Id = "rewritten-value";

            second.Aggregations[0].Id.Should().Be("brand");
            second.Aggregations[0].Values[0].Id.Should().Be("acme");
            source.Aggregations[0].Values[0].Id.Should().Be("acme");
        }

        [Fact]
        public void CloneSearchResponse_NullAggregationsAndValues_AreCarriedThrough()
        {
            // Values has no initializer, so null is its default and a real response can carry it.
            var handler = GetHandler();

            handler.CallCloneSearchResponse(new SearchResponse { Aggregations = null }).Aggregations.Should().BeNull();

            var withNullValues = new SearchResponse { Aggregations = [new AggregationResponse { Id = "brand", Values = null }] };

            handler.CallCloneSearchResponse(withNullValues).Aggregations[0].Values.Should().BeNull();
        }

        [Fact]
        public void CloneSearchResponse_DerivedSourceWithDefaultImplementation_DoesNotPreserveDerivedState()
        {
            // Stated as the documented contract, not as a guarantee the mechanism can give: neither plain
            // construction nor AbstractTypeFactory can carry a derived type's own fields. DerivedSearchResponse
            // is deliberately unknown to the factory, so this cannot pass by coincidence.
            var handler = GetHandler();
            var source = new DerivedSearchResponse { TotalCount = 5, Marker = "derived" };

            var clone = handler.CallCloneSearchResponse(source);

            clone.Should().NotBeOfType<DerivedSearchResponse>();
            clone.TotalCount.Should().Be(5, "base state is still copied");
        }

        [Fact]
        public void CloneSearchResponse_DerivedSourceWithOverriddenClone_PreservesDerivedState()
        {
            var handler = new DerivedAwareHandler(
                _searchProviderMock.Object,
                _mapperMock.Object,
                _storeCurrencyResolverMock.Object,
                _storeServiceMock.Object,
                _pipelineMock.Object,
                _aggregationConverterMock.Object,
                _phraseParserMock.Object,
                _productSortingServiceMock.Object,
                _propertyServiceMock.Object,
                Mock.Of<IRequestScopedCacheAccessor>());

            var clone = handler.CallCloneSearchResponse(new DerivedSearchResponse { TotalCount = 5, Marker = "derived" });

            clone.Should().BeOfType<DerivedSearchResponse>();
            ((DerivedSearchResponse)clone).Marker.Should().Be("derived");
        }

        [Fact]
        public void BuildSearchCacheKey_AndFilterVersusOrFilterOverIdenticalChildren_AreDistinct()
        {
            var handler = GetHandler();
            var and = new SearchRequest { Filter = new AndFilter { ChildFilters = Children() } };
            var or = new SearchRequest { Filter = new OrFilter { ChildFilters = Children() } };

            handler.CallBuildSearchCacheKey(and).Should().NotBe(handler.CallBuildSearchCacheKey(or));
        }

        [Fact]
        public void BuildSearchCacheKey_WithoutTypeDiscriminator_AndFilterAndOrFilterCollide()
        {
            // Fixture check for the test above: AndFilter and OrFilter each declare only ChildFilters, so
            // without $type they really are indistinguishable. That makes the pass above evidence about the
            // discriminator rather than about some incidental difference between the two filter types.
            var withoutDiscriminator = JsonHashExtensions.CreateCacheKeySettings();
            withoutDiscriminator.TypeNameHandling = TypeNameHandling.None;

            var and = new SearchRequest { Filter = new AndFilter { ChildFilters = Children() } };
            var or = new SearchRequest { Filter = new OrFilter { ChildFilters = Children() } };

            and.GetJsonSha256Hex(withoutDiscriminator).Should().Be(or.GetJsonSha256Hex(withoutDiscriminator));
        }

        [Fact]
        public void BuildSearchCacheKey_DifferingObjectIdsOrder_AreDistinct()
        {
            // ObjectIds order is meaningful - it drives IdsFilter.Values and Take - so it is deliberately NOT
            // canonicalised, and two orders must key differently.
            var handler = GetHandler();
            var first = new SearchRequest { Filter = new IdsFilter { Values = ["a", "b"] } };
            var second = new SearchRequest { Filter = new IdsFilter { Values = ["b", "a"] } };

            handler.CallBuildSearchCacheKey(first).Should().NotBe(handler.CallBuildSearchCacheKey(second));
        }

        [Fact]
        public async Task Handle_SeamOverridden_ServesBothQueryTypesWithoutTouchingTheProvider()
        {
            // The "one call site" claim, checked behaviourally. A unit test cannot prove a class contains
            // exactly one call site; it can prove nothing bypasses the seam at runtime.
            var handler = new SeamOverridingHandler(
                _searchProviderMock.Object,
                _mapperMock.Object,
                _storeCurrencyResolverMock.Object,
                _storeServiceMock.Object,
                _pipelineMock.Object,
                _aggregationConverterMock.Object,
                _phraseParserMock.Object,
                _productSortingServiceMock.Object,
                _propertyServiceMock.Object,
                Mock.Of<IRequestScopedCacheAccessor>());

            _mapperMock
                .Setup(x => x.Map<SearchProductQuery>(It.IsAny<LoadProductsQuery>()))
                .Returns(() => Query("mapped"));

            await handler.Handle(Query("direct"), CancellationToken.None);
            await handler.Handle(new LoadProductsQuery { StoreId = DEFAULT_STORE_ID, ObjectIds = ["p-1"] }, CancellationToken.None);

            handler.SeamCalls.Should().Be(2);
            _capturedSearchRequests.Should().BeEmpty("every provider access goes through the seam");
        }

        private static IList<IFilter> Children() =>
        [
            new TermFilter { FieldName = "color", Values = ["red"] },
            new TermFilter { FieldName = "size", Values = ["m"] },
        ];

        // The certain date lands as the upper bound of the "startdate" range filter, written with "O".
        private static string CertainDateOf(SearchRequest request) =>
            FindRangeFilters(request.Filter)
                .FirstOrDefault(x => x.FieldName == START_DATE_FIELD)?
                .Values?.FirstOrDefault()?.Upper;

        private static IEnumerable<RangeFilter> FindRangeFilters(IFilter filter)
        {
            switch (filter)
            {
                case RangeFilter range:
                    yield return range;
                    break;

                case AndFilter and:
                    foreach (var found in and.ChildFilters.SelectMany(FindRangeFilters))
                    {
                        yield return found;
                    }

                    break;

                case OrFilter or:
                    foreach (var found in or.ChildFilters.SelectMany(FindRangeFilters))
                    {
                        yield return found;
                    }

                    break;
            }
        }

        private sealed class DerivedSearchResponse : SearchResponse
        {
            public string Marker { get; set; }
        }

        private class TestableHandler : SearchProductQueryHandler
        {
            public TestableHandler(
                ISearchProvider searchProvider,
                IMapper mapper,
                IStoreCurrencyResolver storeCurrencyResolver,
                IStoreService storeService,
                IGenericPipelineLauncher pipeline,
                IAggregationConverter aggregationConverter,
                ISearchPhraseParser phraseParser,
                IProductSortingService productSortingService,
                IPropertyService propertyService,
                IRequestScopedCacheAccessor requestScopedCacheAccessor)
                : base(searchProvider, mapper, storeCurrencyResolver, storeService, pipeline, aggregationConverter, phraseParser, productSortingService, propertyService, requestScopedCacheAccessor)
            {
            }

            public Task<SearchResponse> CallSearchProductsAsync(SearchRequest searchRequest) => SearchProductsAsync(searchRequest);

            public SearchResponse CallCloneSearchResponse(SearchResponse source) => CloneSearchResponse(source);

            public string CallBuildSearchCacheKey(SearchRequest searchRequest) => BuildSearchCacheKey(searchRequest);
        }

        private sealed class DerivedAwareHandler : TestableHandler
        {
            public DerivedAwareHandler(
                ISearchProvider searchProvider,
                IMapper mapper,
                IStoreCurrencyResolver storeCurrencyResolver,
                IStoreService storeService,
                IGenericPipelineLauncher pipeline,
                IAggregationConverter aggregationConverter,
                ISearchPhraseParser phraseParser,
                IProductSortingService productSortingService,
                IPropertyService propertyService,
                IRequestScopedCacheAccessor requestScopedCacheAccessor)
                : base(searchProvider, mapper, storeCurrencyResolver, storeService, pipeline, aggregationConverter, phraseParser, productSortingService, propertyService, requestScopedCacheAccessor)
            {
            }

            protected override SearchResponse CloneSearchResponse(SearchResponse source)
            {
                if (source is not DerivedSearchResponse derived)
                {
                    return base.CloneSearchResponse(source);
                }

                return new DerivedSearchResponse
                {
                    TotalCount = derived.TotalCount,
                    Documents = derived.Documents,
                    Aggregations = derived.Aggregations,
                    Marker = derived.Marker,
                };
            }
        }

        private sealed class SeamOverridingHandler : TestableHandler
        {
            public SeamOverridingHandler(
                ISearchProvider searchProvider,
                IMapper mapper,
                IStoreCurrencyResolver storeCurrencyResolver,
                IStoreService storeService,
                IGenericPipelineLauncher pipeline,
                IAggregationConverter aggregationConverter,
                ISearchPhraseParser phraseParser,
                IProductSortingService productSortingService,
                IPropertyService propertyService,
                IRequestScopedCacheAccessor requestScopedCacheAccessor)
                : base(searchProvider, mapper, storeCurrencyResolver, storeService, pipeline, aggregationConverter, phraseParser, productSortingService, propertyService, requestScopedCacheAccessor)
            {
            }

            public int SeamCalls { get; private set; }

            protected override Task<SearchResponse> SearchProductsAsync(SearchRequest searchRequest)
            {
                SeamCalls++;

                return Task.FromResult(new SearchResponse());
            }
        }
    }
}
