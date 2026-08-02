using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Search.Sorting;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Extensions;
using VirtoCommerce.XCatalog.Data.Index;
using Aggregation = VirtoCommerce.CatalogModule.Core.Model.Search.Aggregation;
using CatalogProductSorting = VirtoCommerce.CatalogModule.Core.Search.Sorting.ProductSorting;
using XapiProductSorting = VirtoCommerce.XCatalog.Core.Models.ProductSorting;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class SearchProductQueryHandler :
        IQueryHandler<SearchProductQuery, SearchProductResponse>,
        IQueryHandler<LoadProductsQuery, LoadProductResponse>
    {
        private readonly IMapper _mapper;
        private readonly ISearchProvider _searchProvider;
        private readonly IStoreCurrencyResolver _storeCurrencyResolver;
        private readonly IStoreService _storeService;
        private readonly IGenericPipelineLauncher _pipeline;
        private readonly IAggregationConverter _aggregationConverter;
        private readonly ISearchPhraseParser _phraseParser;
        private readonly IProductSortingService _productSortingService;
        private readonly IPropertyService _propertyService;
        private readonly IRequestScopedCacheAccessor _requestScopedCacheAccessor;

        // Set in Handle() before GetIndexedSearchRequestBuilder() is called; read by that method's own
        // WithMultilanguageProperties() call. Keeps the method's signature stable for existing overrides.
        protected IEnumerable<string> MultilanguagePropertyNames { get; set; } = [];

        // Same arrangement, for the instant the validity-window filters are evaluated at. Null means "no
        // request pinned one", which reads as UtcNow at the point of use.
        protected DateTime? CertainDate { get; set; }

        public SearchProductQueryHandler(
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
        {
            _searchProvider = searchProvider;
            _mapper = mapper;
            _storeCurrencyResolver = storeCurrencyResolver;
            _storeService = storeService;
            _pipeline = pipeline;
            _aggregationConverter = aggregationConverter;
            _phraseParser = phraseParser;
            _productSortingService = productSortingService;
            _propertyService = propertyService;
            _requestScopedCacheAccessor = requestScopedCacheAccessor;
        }

        // Both obsolete overloads target the primary constructor directly rather than chaining through each
        // other: a chain would route a caller of the oldest one through a member that is itself deprecated.
        // The DiagnosticId names the release wave the deprecation belongs to, so it is shared with every
        // other member deprecated in the same release - it is not allocated per site.
        [Obsolete("Use the constructor overload with IRequestScopedCacheAccessor to deduplicate identical searches within one request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
        public SearchProductQueryHandler(
            ISearchProvider searchProvider,
            IMapper mapper,
            IStoreCurrencyResolver storeCurrencyResolver,
            IStoreService storeService,
            IGenericPipelineLauncher pipeline,
            IAggregationConverter aggregationConverter,
            ISearchPhraseParser phraseParser,
            IProductSortingService productSortingService,
            IPropertyService propertyService)
            : this(searchProvider, mapper, storeCurrencyResolver, storeService, pipeline, aggregationConverter, phraseParser, productSortingService, propertyService, null)
        {
        }

        [Obsolete("Use the constructor overload with IPropertyService to enable multilanguage property filtering.", DiagnosticId = "VC0016", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
        public SearchProductQueryHandler(
            ISearchProvider searchProvider,
            IMapper mapper,
            IStoreCurrencyResolver storeCurrencyResolver,
            IStoreService storeService,
            IGenericPipelineLauncher pipeline,
            IAggregationConverter aggregationConverter,
            ISearchPhraseParser phraseParser,
            IProductSortingService productSortingService)
            : this(searchProvider, mapper, storeCurrencyResolver, storeService, pipeline, aggregationConverter, phraseParser, productSortingService, null, null)
        {
        }

        public virtual async Task<LoadProductResponse> Handle(LoadProductsQuery request, CancellationToken cancellationToken)
        {
            var searchRequest = _mapper.Map<SearchProductQuery>(request);

            var result = await Handle(searchRequest, cancellationToken);

            return new LoadProductResponse(result.Results);
        }

        /// <summary>
        /// Handle search products query and return search result with facets
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task<SearchProductResponse> Handle(SearchProductQuery request, CancellationToken cancellationToken)
        {
            var allStoreCurrencies = await _storeCurrencyResolver.GetAllStoreCurrenciesAsync(request.StoreId, request.CultureName);
            var currency = await _storeCurrencyResolver.GetStoreCurrencyAsync(request.CurrencyCode, request.StoreId, request.CultureName);
            var store = await _storeService.GetByIdAsync(request.StoreId);
            var responseGroup = EnumUtility.SafeParse(request.GetResponseGroup(), ExpProductResponseGroup.None);

            var languageCode = store.Languages.Contains(request.CultureName) ? request.CultureName : store.DefaultLanguage;

            MultilanguagePropertyNames = _propertyService != null
                ? (await _propertyService.GetAllCatalogPropertiesAsync(store.Catalog)).GetMultilanguagePropertyNames()
                : [];

            CertainDate = await ResolveCertainDateAsync();

            // Sortings are resolved further down (after the filter is parsed into the request builder), so a
            // resolver can read the current category (id / outline) rather than scraping the raw filter string.
            IList<CatalogProductSorting> sortings = null;
            CatalogProductSorting selectedSorting = null;
            string[] categoryOutlines = null;

            var builder = GetIndexedSearchRequestBuilder(request, store, currency);

            var criteria = new ProductIndexedSearchCriteria
            {
                StoreId = request.StoreId,
                Currency = request.CurrencyCode ?? store.DefaultCurrency,
                LanguageCode = languageCode,
                CatalogId = store.Catalog,
            };

            // The filter is now parsed into the builder, so the browsed category (outline) is available. Resolve the
            // chosen sorting (empty sort -> store default; raw expression -> passthrough) and then apply sorting.
            // Skipped on the load-by-ids path so it preserves the requested order.
            var sortToApply = request.Sort;

            if (request.ObjectIds.IsNullOrEmpty())
            {
                // Parse the browsed category outline once; reused by ApplyOutlineCriteria below (the request
                // pipeline does not modify __outline filters), so outlines are not parsed twice per search.
                categoryOutlines = GetOutlines(builder.Build());
                var categoryOutline = categoryOutlines.MaxBy(x => x.Length);
                var currentCategoryId = GetCurrentCategoryId(categoryOutline);

                // Bind the logical "priority" sort to the browsed category's merchandising field (Featured ordering).
                builder.WithCategory(currentCategoryId);

                sortings = await _productSortingService.GetSortingsAsync(new ProductSortingContext
                {
                    StoreId = request.StoreId,
                    CatalogId = store.Catalog,
                    Outline = categoryOutline,
                    CategoryId = currentCategoryId,
                    CurrencyCode = currency.Code,
                    CultureName = languageCode,
                    Sort = request.Sort,
                    Keyword = request.Query,
                    Filter = request.Filter,
                    Facet = request.Facet,
                });
                selectedSorting = sortings.FindSelected(request.Sort);
                sortToApply = selectedSorting?.SortExpression ?? request.Sort;
            }

            builder.AddSorting(sortToApply);

            //Use predefined  facets for store  if the facet filter expression is not set
            if (responseGroup.HasFlag(ExpProductResponseGroup.LoadFacets))
            {
                var predefinedAggregations = await _aggregationConverter.GetAggregationRequestsAsync(criteria, new FiltersContainer());

                builder.ParseFacets(_phraseParser, request.Facet, predefinedAggregations)
                   .ApplyMultiSelectFacetSearch();
            }

            await _pipeline.Execute(builder);

            var searchRequest = builder.Build();

            // Enrich criteria with outlines to filter outline aggregation items and return only child elements.
            // Reuse the outlines already parsed during sort resolution when available (load-by-ids parses on demand).
            if (categoryOutlines != null)
            {
                criteria.Outlines = categoryOutlines;
            }

            ApplyOutlineCriteria(criteria, searchRequest);

            var searchResult = await SearchProductsAsync(searchRequest);

            var resultAggregations = await ConvertAggregations(searchResult, searchRequest, criteria);

            // Mark applied aggregation items
            searchRequest.SetAppliedAggregations(resultAggregations);

            var result = OverridableType<SearchProductResponse>.New();
            result.Query = request;
            result.UserFilters = builder.UserFilters;
            result.GeneratedFilters = builder.GeneratedFilters;
            result.AllStoreCurrencies = allStoreCurrencies;
            result.Currency = currency;
            result.Store = store;
            result.Results = ConvertProducts(searchResult);
            result.Facets = ApplyFacetLocalization(resultAggregations, criteria.LanguageCode);
            result.TotalCount = (int)searchResult.TotalCount;
            result.Sortings = BuildSortings(sortings, selectedSorting, languageCode);

            await _pipeline.Execute(result);

            return result;
        }

        /// <summary>
        /// The single point through which this handler reaches the search provider, so that both query types
        /// it serves - <see cref="SearchProductQuery"/> and <see cref="LoadProductsQuery"/>, the latter
        /// re-entering through the former - go through one overridable call.
        /// </summary>
        /// <remarks>
        /// An override replacing this with a caching implementation takes on four obligations, none of which
        /// the compiler or a passing test will remind it about:
        /// <br/><br/>
        /// <b>Key completeness.</b> The response is a pure function of the two arguments
        /// <see cref="ISearchProvider.SearchAsync"/> receives, so a key derived from the whole
        /// <see cref="SearchRequest"/> is complete by construction. Enumerating selected fields instead is
        /// how a key silently goes under-inclusive - note that entitlement reaches the provider only as
        /// filters written into <see cref="SearchRequest.Filter"/>, not as anything the builder carries
        /// alongside. Anything read from ambient state rather than from the request must be in the key too.
        /// <br/><br/>
        /// <b>Response isolation.</b> Callers mutate what they receive - the aggregation converter rewrites
        /// ids on <c>AggregationResponseValue</c> in place, and <c>SetAppliedAggregations</c> writes back to
        /// the request. A cached entry handed out twice unmodified therefore corrupts the second caller. Copy
        /// on the way out, on <b>both</b> paths: a store-the-task cache hands the first caller the very
        /// instance every later caller gets.
        /// <br/><br/>
        /// <b>Derived state.</b> The copy must carry whatever a derived response type adds. The default copy
        /// below cannot do that for a type it does not know, and
        /// <c>AbstractTypeFactory.TryCreateInstance</c> cannot either - it resolves by the base type's name
        /// and copies no state. A derived type is the overrider's to preserve.
        /// <br/><br/>
        /// <b>Failure semantics.</b> A faulted search must not be retried for the rest of the request; the
        /// request-scoped cache stores the faulted task and rethrows it, which is the intended behaviour.
        /// <br/><br/>
        /// One obligation the default copy below does NOT discharge, because it cannot: <c>Documents</c> is
        /// shared by reference, and a <c>SearchDocument</c> is a mutable dictionary. Nothing on this path
        /// writes to one - the binders only read, and each caller gets its own <c>ExpProduct</c> - but a
        /// field binder that hands back a reference-typed value out of a document, which the caller then
        /// mutates, corrupts every other holder of that cached entry. Treat documents as read-only.
        /// </remarks>
        protected virtual Task<SearchResponse> SearchProductsAsync(SearchRequest searchRequest)
        {
            var cache = _requestScopedCacheAccessor?.Cache;

            // No ambient request scope: nothing to bound the entry to, so the caller gets the provider's own
            // instance - the behaviour this handler had before the cache existed.
            if (cache is null)
            {
                return _searchProvider.SearchAsync(KnownDocumentTypes.Product, searchRequest);
            }

            return SearchProductsThroughCacheAsync(cache, searchRequest);
        }

        private async Task<SearchResponse> SearchProductsThroughCacheAsync(IRequestScopedCache cache, SearchRequest searchRequest)
        {
            var response = await cache.GetOrAddAsync(
                BuildSearchCacheKey(searchRequest),
                () => _searchProvider.SearchAsync(KnownDocumentTypes.Product, searchRequest));

            // Copy on the miss as well as the hit. The cache stores the TASK the factory returned, so the
            // caller that populated the entry holds the very instance every later caller will be handed;
            // cloning only on the hit would leave the first caller aliased to all the others.
            return CloneSearchResponse(response);
        }

        /// <summary>
        /// Key for one provider call. Complete by construction: <see cref="ISearchProvider.SearchAsync"/>
        /// takes exactly the document type and the request, so hashing the whole request covers every input.
        /// </summary>
        /// <remarks>
        /// The hash is over the request as a graph, not over selected fields - a hand-written projection goes
        /// silently under-inclusive the day upstream adds a field, and serves the wrong documents. Note that
        /// <c>ObjectIds</c> order is NOT canonicalised: it drives <c>IdsFilter.Values</c> and <c>Take</c>, so
        /// two orders are two different calls and hashing verbatim is correct.
        /// <br/><br/>
        /// Scoped by the runtime type's FULL name, so a subclass that alters the search cannot collide with
        /// the base handler's entries. <c>CacheKey.With(Type, ...)</c> alone would not give that: it renders
        /// the type through <c>PrettyPrint</c>, which for a non-generic type yields the SHORT name, so a
        /// subclass keeping the name <c>SearchProductQueryHandler</c> in its own namespace would key
        /// identically to this one.
        /// </remarks>
        protected virtual string BuildSearchCacheKey(SearchRequest searchRequest)
        {
            return CacheKey.With(GetType(), GetType().FullName, nameof(SearchProductsAsync), KnownDocumentTypes.Product, searchRequest.GetJsonSha256Hex());
        }

        /// <summary>
        /// A per-caller copy of a response that may be handed out more than once.
        /// </summary>
        /// <remarks>
        /// Deep only where something downstream writes. <c>AggregationResponse.Id</c> and the ids on its
        /// <c>Values</c> are both rewritten in place by the aggregation converter's range handling, so
        /// sharing either would let one caller's conversion rewrite another's data - the values are the
        /// half that is easy to miss, because copying only the <c>AggregationResponse</c> looks complete.
        /// <c>Documents</c> and <c>Statistics</c> are shared deliberately: nothing on this path writes to
        /// them, and documents are the large part of a response.
        /// <br/><br/>
        /// Deliberately <c>new SearchResponse()</c> rather than <c>AbstractTypeFactory</c>: that factory
        /// resolves by the base type's NAME, so it would return the registered override type for a source
        /// that is a plain base instance, and it copies no state either way. Neither plain construction nor
        /// the factory can preserve a derived type - hence this is <c>virtual</c>, and preserving derived
        /// state is stated as the overrider's obligation on <see cref="SearchProductsAsync"/>.
        /// </remarks>
        protected virtual SearchResponse CloneSearchResponse(SearchResponse source)
        {
            return new SearchResponse
            {
                TotalCount = source.TotalCount,
                Documents = source.Documents,
                Aggregations = source.Aggregations?.Select(CloneAggregationResponse).ToList(),
            };
        }

        /// <summary>
        /// Copy of one aggregation, deep through its values. <c>Values</c> has no initializer, so null is
        /// its default and a real response can carry it.
        /// </summary>
        protected virtual AggregationResponse CloneAggregationResponse(AggregationResponse source)
        {
            return new AggregationResponse
            {
                Id = source.Id,
                Statistics = source.Statistics,
                Values = source.Values?.Select(x => new AggregationResponseValue { Id = x.Id, Count = x.Count }).ToList(),
            };
        }

        // A cache keyed on a request that embeds a clock reading can never hit. AddCertainDateFilter writes
        // the value as "O" - 100-nanosecond precision - into the filter tree, so two otherwise identical
        // searches in one request differ on it alone and every lookup misses: no error, no log, just a cache
        // that never helps. Pinning one instant per request is the precondition for the deduplication below,
        // not an optimisation on top of it.
        //
        // No ambient request scope (a background job, startup) means nothing to bound the value to, so each
        // send reads its own clock - which is the behaviour that existed before this method.
        private Task<DateTime> ResolveCertainDateAsync()
        {
            var cache = _requestScopedCacheAccessor?.Cache;

            if (cache is null)
            {
                return Task.FromResult(DateTime.UtcNow);
            }

            return cache.GetOrAddAsync(ModuleConstants.CertainDateRequestCacheKey, () => Task.FromResult(DateTime.UtcNow));
        }

        protected virtual IndexSearchRequestBuilder GetIndexedSearchRequestBuilder(SearchProductQuery request, Store store, CoreModule.Core.Currency.Currency currency)
        {
            var builder = new IndexSearchRequestBuilder()
                                            .WithStoreId(request.StoreId)
                                            .WithUserId(request.UserId)
                                            .WithOrganizationId(request.OrganizationId)
                                            .WithCatalog(store.Catalog)
                                            .WithCurrency(currency.Code)
                                            .WithFuzzy(request.Fuzzy, request.FuzzyLevel)
                                            .AddCertainDateFilter(CertainDate ?? DateTime.UtcNow)
                                            .WithMultilanguageProperties(MultilanguagePropertyNames)
                                            .WithCultureName(request.CultureName)
                                            .ParseFilters(_phraseParser, request.Filter)
                                            .WithSearchPhrase(request.Query)
                                            .WithPreserveUserQuery(request.PreserveUserQuery)
                                            .WithPaging(request.Skip, request.Take)
                                            .AddObjectIds(request.ObjectIds)
                                            .WithIncludeFields(IndexFieldsMapper.MapToIndexIncludes(request.IncludeFields).ToArray());

            if (request.ObjectIds.IsNullOrEmpty())
            {
                AddDefaultTerms(builder, store.Catalog);
            }

            return builder;
        }

        protected virtual void ApplyOutlineCriteria(ProductIndexedSearchCriteria criteria, SearchRequest searchRequest)
        {
            criteria.Outlines ??= GetOutlines(searchRequest);
            criteria.Outline = criteria.Outlines.MaxBy(x => x.Length);
        }

        private static string[] GetOutlines(SearchRequest searchRequest)
        {
            return searchRequest.GetChildFilters()
                .Where(f => f is TermFilter && f.GetFieldName() == "__outline")
                .SelectMany(f => ((TermFilter)f).Values)
                .Where(o => !string.IsNullOrEmpty(o))
                .ToArray();
        }

        private static string GetCurrentCategoryId(string outline)
        {
            // outline = "catalogId/.../currentCategoryId"; the leaf segment is the category being browsed
            // (null when browsing the catalog root, where the outline is just the catalog id or absent).
            var segments = outline?.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments?.Length > 1 ? segments[^1] : null;
        }

        protected virtual Task<Aggregation[]> ConvertAggregations(SearchResponse searchResponse, SearchRequest searchRequest, ProductIndexedSearchCriteria criteria)
        {
            // Call the catalog aggregation converter service to convert AggregationResponse to proper Aggregation type (term, range, filter)
            return _aggregationConverter.ConvertAggregationsAsync(searchResponse.Aggregations, criteria);
        }

        protected virtual IList<ExpProduct> ConvertProducts(SearchResponse searchResponse)
        {
            return searchResponse.Documents?.Select(x => _mapper.Map<ExpProduct>(x)).ToList() ?? new List<ExpProduct>();
        }

        protected virtual IList<FacetResult> ApplyFacetLocalization(Aggregation[] resultAggregations, string languageCode)
        {
            return resultAggregations
                .ApplyLanguageSpecificFacetResult(languageCode)
                .Select(x => _mapper.Map<FacetResult>(x, options =>
                {
                    options.Items["cultureName"] = languageCode;
                    options.Items["order"] = Array.IndexOf(resultAggregations, x);
                }))
                .ToList();
        }

        protected virtual IList<XapiProductSorting> BuildSortings(IList<CatalogProductSorting> sortings, CatalogProductSorting selected, string languageCode)
        {
            return (sortings ?? [])
                .Where(x => x.IsVisible)
                .Select(x => new XapiProductSorting
                {
                    Id = x.Code,
                    Name = ResolveSortingName(x, languageCode),
                    IsDefault = x.IsDefault,
                    IsSelected = selected != null && x.Code.EqualsIgnoreCase(selected.Code),
                })
                .ToList();
        }

        private static string ResolveSortingName(CatalogProductSorting sorting, string languageCode)
        {
            if (!string.IsNullOrEmpty(languageCode) &&
                sorting.LocalizedNames != null &&
                sorting.LocalizedNames.TryGetValue(languageCode, out var localizedName) &&
                !string.IsNullOrEmpty(localizedName))
            {
                return localizedName;
            }

            return sorting.Name;
        }

        /// <summary>
        /// By default limit  resulting products, return only visible products and belongs to store catalog,
        /// but user can override this behavior by passing "status:hidden" and/or "is:variation" in a filter expression
        /// </summary>
        /// <param name="builder">Instance of the request builder</param>
        /// <param name="catalog">Name of the current catalog</param>
        protected virtual void AddDefaultTerms(IndexSearchRequestBuilder builder, string catalog)
        {
            builder.AddTermFilter("is", "product", skipIfExists: true);
            builder.AddTermFilter("status", "visible", skipIfExists: true);
            builder.AddTermFilter("__outline", catalog);
        }
    }
}
