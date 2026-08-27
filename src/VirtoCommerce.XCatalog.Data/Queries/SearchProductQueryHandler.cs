using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using VirtoCommerce.XCatalog.Core.Services;
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
        private readonly IXCatalogMapper _mapper;
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
            IXCatalogMapper mapper,
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

        [Obsolete("Use the constructor overload with IRequestScopedCacheAccessor to deduplicate identical searches within one request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
        public SearchProductQueryHandler(
            ISearchProvider searchProvider,
            IXCatalogMapper mapper,
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
            IXCatalogMapper mapper,
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
            var searchRequest = _mapper.ToSearchProductQuery(request);

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

            var result = AbstractTypeFactory<SearchProductResponse>.TryCreateInstance();
            result.Query = request;
            result.UserFilters = builder.UserFilters;
            result.GeneratedFilters = builder.GeneratedFilters;
            result.AllStoreCurrencies = allStoreCurrencies;
            result.Currency = currency;
            result.Store = store;
            result.Results = ConvertProducts(searchResult);
            result.Facets = ApplyFacetLocalization(resultAggregations, result, criteria.LanguageCode);
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
        /// <para>
        /// An override that caches inherits two obligations the compiler and a passing test are both silent
        /// about: the key must cover every input (see <see cref="BuildSearchCacheKey"/>), and every caller must
        /// get its own copy (see <see cref="CloneSearchResponse"/>). The clone chain constructs through
        /// <c>AbstractTypeFactory</c>, so a registered override type already comes back from it - what the base
        /// cannot copy is the state that type adds.
        /// </para>
        /// <para>
        /// <c>Documents</c> is shared by reference and a <c>SearchDocument</c> is a mutable dictionary.
        /// Nothing on this path writes to one, so treat them as read-only: a field binder that hands a
        /// reference-typed value out of a document to a caller that then mutates it corrupts every other
        /// holder of that entry.
        /// </para>
        /// </remarks>
        protected virtual async Task<SearchResponse> SearchProductsAsync(SearchRequest searchRequest)
        {
            var cache = _requestScopedCacheAccessor?.Cache;

            // No ambient request scope: nothing to bound the entry to, so the caller gets the provider's own
            // instance - the behaviour this handler had before the cache existed.
            if (cache is null)
            {
                return await _searchProvider.SearchAsync(KnownDocumentTypes.Product, searchRequest);
            }

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
        /// silently under-inclusive the day upstream adds a field, and serves the wrong documents.
        /// <c>ObjectIds</c> order is deliberately NOT canonicalised: it drives <c>IdsFilter.Values</c> and
        /// <c>Take</c>, so two orders are two different calls. <c>GetType()</c> scopes the key so a subclass
        /// that alters the search keys separately from the base handler.
        /// </remarks>
        protected virtual string BuildSearchCacheKey(SearchRequest searchRequest)
        {
            return CacheKey.With(GetType(), nameof(SearchProductsAsync), KnownDocumentTypes.Product, searchRequest.GetJsonSha256Hex());
        }

        /// <summary>
        /// A per-caller copy of a response that may be handed out more than once.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Deep only where something downstream writes, and the aggregation converter writes at both levels:
        /// it replaces <c>AggregationResponse.Values</c> in place when it filters outlines, and mutates
        /// <c>AggregationResponseValue.Id</c> in place in its range handling. The second is the one that is
        /// easy to miss, because copying only the <c>AggregationResponse</c> looks complete.
        /// <c>Documents</c> is shared deliberately - nothing on this path writes to one, and documents are
        /// the large part of a response.
        /// </para>
        /// </remarks>
        protected virtual SearchResponse CloneSearchResponse(SearchResponse source)
        {
            var clone = AbstractTypeFactory<SearchResponse>.TryCreateInstance();

            clone.TotalCount = source.TotalCount;
            clone.Documents = source.Documents;
            clone.Aggregations = source.Aggregations?.Select(CloneAggregationResponse).ToList();

            return clone;
        }

        protected virtual AggregationResponse CloneAggregationResponse(AggregationResponse source)
        {
            var clone = AbstractTypeFactory<AggregationResponse>.TryCreateInstance();

            clone.Id = source.Id;
            clone.Statistics = source.Statistics;
            clone.Values = source.Values?.Select(CloneAggregationResponseValue).ToList();

            return clone;
        }

        protected virtual AggregationResponseValue CloneAggregationResponseValue(AggregationResponseValue source)
        {
            var clone = AbstractTypeFactory<AggregationResponseValue>.TryCreateInstance();

            clone.Id = source.Id;
            clone.Count = source.Count;

            return clone;
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
            return searchResponse.Documents?.Select(_mapper.ToExpProduct).ToList() ?? [];
        }

        protected virtual IList<FacetResult> ApplyFacetLocalization(Aggregation[] resultAggregations, SearchProductResponse response, string languageCode)
        {
            var context = CreateFacetMappingContext(response, languageCode);

            return resultAggregations
                .ApplyLanguageSpecificFacetResult(languageCode)
                .Select(x =>
                {
                    var result = _mapper.ToFacetResult(x, context);
                    if (result != null)
                    {
                        result.Order = Array.IndexOf(resultAggregations, x);
                    }

                    return result;
                })
                .ToList();
        }

        /// <summary>
        /// <paramref name="languageCode"/> is the store-resolved value <see cref="ApplyFacetLocalization"/>
        /// also uses for <c>ApplyLanguageSpecificFacetResult</c> - not <c>response.Query.CultureName</c>, the
        /// raw, unresolved request value. Passing the resolved value in keeps both halves of one operation
        /// on the same language instead of the carrier's raw field silently diverging from it.
        /// </summary>
        protected virtual CatalogFacetMappingContext CreateFacetMappingContext(SearchProductResponse response, string languageCode)
        {
            var context = AbstractTypeFactory<CatalogFacetMappingContext>.TryCreateInstance();
            context.CultureName = languageCode;
            context.CurrencyCode = response.Query.CurrencyCode;

            return context;
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
