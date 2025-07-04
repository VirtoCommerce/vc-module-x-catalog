using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
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

        public SearchProductQueryHandler(
            ISearchProvider searchProvider,
            IMapper mapper,
            IStoreCurrencyResolver storeCurrencyResolver,
            IStoreService storeService,
            IGenericPipelineLauncher pipeline,
            IAggregationConverter aggregationConverter,
            ISearchPhraseParser phraseParser)
        {
            _searchProvider = searchProvider;
            _mapper = mapper;
            _storeCurrencyResolver = storeCurrencyResolver;
            _storeService = storeService;
            _pipeline = pipeline;
            _aggregationConverter = aggregationConverter;
            _phraseParser = phraseParser;
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

            var builder = GetIndexedSearchRequestBuilder(request, store, currency);

            var criteria = new ProductIndexedSearchCriteria
            {
                StoreId = request.StoreId,
                Currency = request.CurrencyCode ?? store.DefaultCurrency,
                LanguageCode = store.Languages.Contains(request.CultureName) ? request.CultureName : store.DefaultLanguage,
                CatalogId = store.Catalog,
            };

            builder.WithCultureName(criteria.LanguageCode);

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
            result.AllStoreCurrencies = allStoreCurrencies;
            result.Currency = currency;
            result.Store = store;
            result.Results = ConvertProducts(searchResult);
            result.Facets = ApplyFacetLocalization(resultAggregations, criteria.LanguageCode);
            result.TotalCount = (int)searchResult.TotalCount;

            await _pipeline.Execute(result);

            return result;
        }

        protected virtual IndexSearchRequestBuilder GetIndexedSearchRequestBuilder(SearchProductQuery request, Store store, CoreModule.Core.Currency.Currency currency)
        {
            var builder = new IndexSearchRequestBuilder()
                                            .WithStoreId(request.StoreId)
                                            .WithUserId(request.UserId)
                                            .WithCurrency(currency.Code)
                                            .WithFuzzy(request.Fuzzy, request.FuzzyLevel)
                                            .AddCertainDateFilter(DateTime.UtcNow)
                                            .ParseFilters(_phraseParser, request.Filter)
                                            .WithSearchPhrase(request.Query)
                                            .WithPaging(request.Skip, request.Take)
                                            .AddObjectIds(request.ObjectIds)
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
            return resultAggregations.ApplyLanguageSpecificFacetResult(languageCode)
                .Select(x => _mapper.Map<FacetResult>(x, options =>
                {
                    options.Items["cultureName"] = languageCode;
                }))
                .ToList();
        }

        /// <summary>
        /// By default limit  resulting products, return only visible products and belongs to store catalog,
        /// but user can override this behavior by passing "status:hidden" and/or "is:variation" in a filter expression
        /// </summary>
        /// <param name="builder">Instance of the request builder</param>
        /// <param name="catalog">Name of the current catalog</param>
        protected virtual void AddDefaultTerms(IndexSearchRequestBuilder builder, string catalog)
        {
            builder.AddTerms(new[] { "is:product" }, skipIfExists: true);
            builder.AddTerms(new[] { "status:visible" }, skipIfExists: true);
            builder.AddTerms(new[] { $"__outline:{catalog}" });
        }
    }
}
