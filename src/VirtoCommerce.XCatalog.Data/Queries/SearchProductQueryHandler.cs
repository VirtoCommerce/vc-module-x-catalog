using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Search.Sorting;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Extensions;
using VirtoCommerce.XCatalog.Data.Index;
using Aggregation = VirtoCommerce.CatalogModule.Core.Model.Search.Aggregation;

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
        private readonly IProductSearchOrderService _productSearchOrderService;

        public SearchProductQueryHandler(
            ISearchProvider searchProvider,
            IMapper mapper,
            IStoreCurrencyResolver storeCurrencyResolver,
            IStoreService storeService,
            IGenericPipelineLauncher pipeline,
            IAggregationConverter aggregationConverter,
            ISearchPhraseParser phraseParser,
            IProductSearchOrderService productSearchOrderService)
        {
            _searchProvider = searchProvider;
            _mapper = mapper;
            _storeCurrencyResolver = storeCurrencyResolver;
            _storeService = storeService;
            _pipeline = pipeline;
            _aggregationConverter = aggregationConverter;
            _phraseParser = phraseParser;
            _productSearchOrderService = productSearchOrderService;
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

            // Resolve the store-level sort orderings and expand the chosen sort code into its sort expression.
            // An empty sort falls back to the store default and a raw expression is passed through unchanged.
            // Skipped on the load-by-ids path so it preserves the requested order.
            IList<ProductSearchOrdering> sortOrderings = null;
            ProductSearchOrdering selectedOrdering = null;
            if (request.ObjectIds.IsNullOrEmpty())
            {
                sortOrderings = await _productSearchOrderService.GetOrderingsAsync(new ProductSearchOrderContext
                {
                    StoreId = request.StoreId,
                    CatalogId = store.Catalog,
                    CurrencyCode = currency.Code,
                    CultureName = languageCode,
                    Sort = request.Sort,
                    Keyword = request.Query,
                    Filter = request.Filter,
                    Facet = request.Facet,
                });
                selectedOrdering = sortOrderings.FindSelected(request.Sort);
                request.Sort = selectedOrdering?.SortExpression ?? request.Sort;
            }

            var builder = GetIndexedSearchRequestBuilder(request, store, currency);

            var criteria = new ProductIndexedSearchCriteria
            {
                StoreId = request.StoreId,
                Currency = request.CurrencyCode ?? store.DefaultCurrency,
                LanguageCode = languageCode,
                CatalogId = store.Catalog,
            };

            //Use predefined  facets for store  if the facet filter expression is not set
            if (responseGroup.HasFlag(ExpProductResponseGroup.LoadFacets))
            {
                var predefinedAggregations = await _aggregationConverter.GetAggregationRequestsAsync(criteria, new FiltersContainer());

                builder.ParseFacets(_phraseParser, request.Facet, predefinedAggregations)
                   .ApplyMultiSelectFacetSearch();
            }

            await _pipeline.Execute(builder);

            var searchRequest = builder.Build();

            // Enrich criteria with outlines to filter outline aggregation items and return only child elements
            ApplyOutlineCriteria(criteria, searchRequest);

            var searchResult = await _searchProvider.SearchAsync(KnownDocumentTypes.Product, searchRequest);

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
            result.SortDefinitions = BuildSortDefinitions(sortOrderings, selectedOrdering, languageCode);

            await _pipeline.Execute(result);

            return result;
        }

        protected virtual IndexSearchRequestBuilder GetIndexedSearchRequestBuilder(SearchProductQuery request, Store store, CoreModule.Core.Currency.Currency currency)
        {
            var builder = new IndexSearchRequestBuilder()
                                            .WithStoreId(request.StoreId)
                                            .WithUserId(request.UserId)
                                            .WithOrganizationId(request.OrganizationId)
                                            .WithCurrency(currency.Code)
                                            .WithFuzzy(request.Fuzzy, request.FuzzyLevel)
                                            .AddCertainDateFilter(DateTime.UtcNow)
                                            .ParseFilters(_phraseParser, request.Filter)
                                            .WithSearchPhrase(request.Query)
                                            .WithPreserveUserQuery(request.PreserveUserQuery)
                                            .WithPaging(request.Skip, request.Take)
                                            .AddObjectIds(request.ObjectIds)
                                            .WithCultureName(request.CultureName)
                                            .AddSorting(request.Sort)
                                            .WithIncludeFields(IndexFieldsMapper.MapToIndexIncludes(request.IncludeFields).ToArray());

            if (request.ObjectIds.IsNullOrEmpty())
            {
                AddDefaultTerms(builder, store.Catalog);
            }

            return builder;
        }

        protected virtual void ApplyOutlineCriteria(ProductIndexedSearchCriteria criteria, SearchRequest searchRequest)
        {
            criteria.Outlines = searchRequest.GetChildFilters()
                .Where(f => f is TermFilter && f.GetFieldName() == "__outline")
                .SelectMany(f => ((TermFilter)f).Values)
                .Where(o => !string.IsNullOrEmpty(o))
                .ToArray();

            criteria.Outline = criteria.Outlines.MaxBy(x => x.Length);
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

        protected virtual IList<ProductSortDefinition> BuildSortDefinitions(IList<ProductSearchOrdering> orderings, ProductSearchOrdering selected, string languageCode)
        {
            return (orderings ?? [])
                .Where(x => x.IsVisible)
                .Select(x => new ProductSortDefinition
                {
                    Id = x.Code,
                    Name = ResolveOrderingName(x, languageCode),
                    IsDefault = x.IsDefault,
                    IsSelected = selected != null && x.Code.EqualsIgnoreCase(selected.Code),
                })
                .ToList();
        }

        private static string ResolveOrderingName(ProductSearchOrdering ordering, string languageCode)
        {
            if (!string.IsNullOrEmpty(languageCode) &&
                ordering.LocalizedNames != null &&
                ordering.LocalizedNames.TryGetValue(languageCode, out var localizedName) &&
                !string.IsNullOrEmpty(localizedName))
            {
                return localizedName;
            }

            return ordering.Name;
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
