using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Services;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class SearchPropertyDictionaryItemQueryHandler : IRequestHandler<SearchPropertyDictionaryItemQuery, SearchPropertyDictionaryItemResponse>
    {
        private readonly IPropertyDictionaryItemSearchService _propertyDictionaryItemSearchService;
        private readonly IXCatalogMapper _mapper;

        public SearchPropertyDictionaryItemQueryHandler(IPropertyDictionaryItemSearchService propertyDictionaryItemSearchService, IXCatalogMapper mapper)
        {
            _mapper = mapper;
            _propertyDictionaryItemSearchService = propertyDictionaryItemSearchService;
        }

        public virtual async Task<SearchPropertyDictionaryItemResponse> Handle(SearchPropertyDictionaryItemQuery request, CancellationToken cancellationToken)
        {
            var result = await _propertyDictionaryItemSearchService.SearchAsync(_mapper.ToPropertyDictionaryItemSearchCriteria(request), clone: false);

            return new SearchPropertyDictionaryItemResponse
            {
                Result = result
            };
        }
    }
}
