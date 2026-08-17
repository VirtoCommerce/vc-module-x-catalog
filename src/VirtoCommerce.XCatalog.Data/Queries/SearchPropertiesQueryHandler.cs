using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Services;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class SearchPropertiesQueryHandler : IRequestHandler<SearchPropertiesQuery, SearchPropertiesResponse>
    {
        private readonly IPropertySearchService _propertySearchService;
        private readonly ISearchPhraseParser _searchPhraseParser;

        public SearchPropertiesQueryHandler(ISearchPhraseParser searchPhraseParser, IPropertySearchService propertySearchService)
        {
            _searchPhraseParser = searchPhraseParser;
            _propertySearchService = propertySearchService;
        }

        public virtual async Task<SearchPropertiesResponse> Handle(SearchPropertiesQuery request, CancellationToken cancellationToken)
        {
            var searchCriteria = new PropertySearchCriteriaBuilder(_searchPhraseParser)
                            .ParseFilters(request.Filter)
                            .WithCatalogId(request.CatalogId)
                            .WithPaging(request.Skip, request.Take)
                            .Build();

            var result = await _propertySearchService.SearchPropertiesAsync(searchCriteria);

            if (request.Types != null)
            {
                result.Results = result.Results.Where(x => request.Types.Contains(x.Type)).ToList();
            }

            return new SearchPropertiesResponse
            {
                Result = result
            };
        }
    }
}
