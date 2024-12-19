using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries;
public class GetProductConfigurationsQueryHandler : IQueryHandler<GetProductConfigurationsQuery, Dictionary<string, bool>>
{
    private readonly IProductConfigurationSearchService _productConfigurationSearchService;

    public GetProductConfigurationsQueryHandler(IProductConfigurationSearchService productConfigurationSearchService)
    {
        _productConfigurationSearchService = productConfigurationSearchService;
    }

    public async Task<Dictionary<string, bool>> Handle(GetProductConfigurationsQuery request, CancellationToken cancellationToken)
    {
        var criteria = AbstractTypeFactory<ProductConfigurationSearchCriteria>.TryCreateInstance();
        criteria.ProductIds = request.ProductIds;

        var configurations = await _productConfigurationSearchService.SearchNoCloneAsync(criteria);

        var result = configurations.Results.ToDictionary(x => x.ProductId, x => x.IsActive);

        var existingConfigurations = result.Select(result => result.Key).ToArray();
        foreach (var nonExistingConfiguration in request.ProductIds.Except(existingConfigurations))
        {
            result.TryAdd(nonExistingConfiguration, false);
        }

        return result;
    }
}
