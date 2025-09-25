using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Services;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetProductPickupLocationsQueryHandler(IOptionalDependency<IProductPickupLocationService> productPickupLocationService)
    : IQueryHandler<SearchProductPickupLocationsQuery, ProductPickupLocationSearchResult>
{
    public async Task<ProductPickupLocationSearchResult> Handle(SearchProductPickupLocationsQuery request, CancellationToken cancellationToken)
    {
        if (productPickupLocationService.Value == null)
        {
            return AbstractTypeFactory<ProductPickupLocationSearchResult>.TryCreateInstance();
        }

        var searchCriteria = AbstractTypeFactory<SingleProductPickupLocationSearchCriteria>.TryCreateInstance();

        searchCriteria.StoreId = request.StoreId;
        searchCriteria.Product = new ProductPickupLocationSearchCriteriaItem() { ProductId = request.ProductId, Quantity = 1 };
        searchCriteria.Keyword = request.Keyword;
        searchCriteria.LanguageCode = request.CultureName;

        searchCriteria.Sort = request.Sort;
        searchCriteria.Skip = request.Skip;
        searchCriteria.Take = request.Take;

        return await productPickupLocationService.Value.SearchPickupLocationsAsync(searchCriteria);
    }
}
