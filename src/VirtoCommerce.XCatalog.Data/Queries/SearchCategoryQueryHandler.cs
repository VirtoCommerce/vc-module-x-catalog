using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Extensions;
using VirtoCommerce.XCatalog.Data.Index;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class SearchCategoryQueryHandler :
        IQueryHandler<SearchCategoryQuery, SearchCategoryResponse>,
        IQueryHandler<LoadCategoryQuery, LoadCategoryResponse>
    {
        private readonly IMapper _mapper;
        private readonly ISearchProvider _searchProvider;
        private readonly ISearchPhraseParser _phraseParser;
        private readonly IStoreService _storeService;
        private readonly IGenericPipelineLauncher _pipeline;
        private readonly IMediator _mediator;

        public SearchCategoryQueryHandler(
            ISearchProvider searchProvider,
            IMapper mapper,
            ISearchPhraseParser phraseParser,
            IStoreService storeService,
            IGenericPipelineLauncher pipeline,
            IMediator mediator)
        {
            _searchProvider = searchProvider;
            _mapper = mapper;
            _phraseParser = phraseParser;
            _storeService = storeService;
            _pipeline = pipeline;
            _mediator = mediator;
        }

        public virtual async Task<SearchCategoryResponse> Handle(SearchCategoryQuery request, CancellationToken cancellationToken)
        {
            var store = await GetStore(request);

            var builder = GetIndexedSearchRequestBuilder(request, store);

            var searchRequest = builder.Build();

            var searchResult = await _searchProvider.SearchAsync(KnownDocumentTypes.Category, searchRequest);

            var categories = searchResult.Documents?.Select(x => _mapper.Map<ExpCategory>(x, options =>
            {
                options.Items["store"] = store;
                options.Items["cultureName"] = request.CultureName;
            })).ToList() ?? [];

            var result = OverridableType<SearchCategoryResponse>.New();
            result.Query = request;
            result.Results = categories;
            result.Store = store;
            result.TotalCount = (int)searchResult.TotalCount;

            await _pipeline.Execute(result);

            if (request.GetLoadChildCategories())
            {
                var childCategoriesQuery = new ChildCategoriesQuery
                {
                    OnlyActive = true,
                    Store = store,
                    StoreId = request.StoreId,
                    UserId = request.UserId,
                    CultureName = request.CultureName,
                    CurrencyCode = request.CurrencyCode,
                };

                var childCategoriesSearchQuery = new SearchCategoryQuery
                {
                    Store = store,
                    StoreId = request.StoreId,
                    UserId = request.UserId,
                    CultureName = request.CultureName,
                    CurrencyCode = request.CurrencyCode,
                };

                var regex = new Regex("^childCategories\\.");

                foreach (var expCategory in result.Results)
                {
                    if (expCategory.ChildCategories != null)
                    {
                        continue;
                    }

                    childCategoriesQuery.CategoryId = expCategory.Id;
                    childCategoriesQuery.MaxLevel = expCategory.Level;

                    var response = await _mediator.Send(childCategoriesQuery, cancellationToken);
                    var categoryIds = response.ChildCategories.Select(x => x.Key).ToArray();

                    if (categoryIds.IsNullOrEmpty())
                    {
                        continue;
                    }

                    childCategoriesSearchQuery.ObjectIds = categoryIds;
                    childCategoriesSearchQuery.Take = categoryIds.Length;
                    childCategoriesSearchQuery.IncludeFields = request.IncludeFields.Where(x => x.StartsWith("childCategories.")).Select(x => regex.Replace(x, string.Empty, 1)).ToList();

                    var childCategories = await Handle(childCategoriesSearchQuery, cancellationToken);

                    expCategory.ChildCategories = childCategories.Results.ToList();
                }
            }

            return result;
        }

        public virtual async Task<LoadCategoryResponse> Handle(LoadCategoryQuery request, CancellationToken cancellationToken)
        {
            var searchRequest = _mapper.Map<SearchCategoryQuery>(request);
            searchRequest.Store = await GetStore(request);

            var result = await Handle(searchRequest, cancellationToken);

            return new LoadCategoryResponse(result.Results);
        }

        protected virtual IndexSearchRequestBuilder GetIndexedSearchRequestBuilder(SearchCategoryQuery request, Store store)
        {
            //Limit search result with store catalog
            var essentialTerms = new List<string>
            {
                $"__outline:{store.Catalog}",
            };

            var searchRequestBuilder = new IndexSearchRequestBuilder()
                .WithFuzzy(request.Fuzzy, request.FuzzyLevel)
                .ParseFilters(_phraseParser, request.Filter)
                .WithSearchPhrase(request.Query)
                .WithPaging(request.Skip, request.Take)
                .AddObjectIds(request.ObjectIds)
                .AddSorting(request.Sort)
                .AddTerms(essentialTerms)
                .WithIncludeFields(IndexFieldsMapper.MapToIndexIncludes(request.IncludeFields).ToArray());

            if (request.ObjectIds.IsNullOrEmpty())
            {
                searchRequestBuilder.AddTerms(["status:visible"], skipIfExists: true);
            }

            return searchRequestBuilder;
        }

        protected async Task<Store> GetStore<T>(CatalogQueryBase<T> request)
        {
            var store = request.Store;

            if (store is null && !string.IsNullOrWhiteSpace(request.StoreId))
            {
                store = await _storeService.GetByIdAsync(request.StoreId)
                    ?? throw new ArgumentException($"Store with Id: {request.StoreId} is absent");
            }

            return store;
        }
    }
}
