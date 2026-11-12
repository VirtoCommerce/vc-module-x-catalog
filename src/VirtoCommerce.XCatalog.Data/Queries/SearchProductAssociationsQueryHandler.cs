using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Services;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    public class SearchProductAssociationsQueryHandler : IRequestHandler<SearchProductAssociationsQuery, SearchProductAssociationsResponse>
    {
        private readonly IProductAssociationSearchService _productAssociationSearchService;
        private readonly IXCatalogMapper _mapper;

        public SearchProductAssociationsQueryHandler(IProductAssociationSearchService productAssociationSearchService, IXCatalogMapper mapper)
        {
            _mapper = mapper;
            _productAssociationSearchService = productAssociationSearchService;
        }

        public virtual async Task<SearchProductAssociationsResponse> Handle(SearchProductAssociationsQuery request, CancellationToken cancellationToken)
        {
            var result = await _productAssociationSearchService.SearchProductAssociationsAsync(_mapper.ToProductAssociationSearchCriteria(request));

            return new SearchProductAssociationsResponse
            {
                Result = result
            };
        }
    }
}
