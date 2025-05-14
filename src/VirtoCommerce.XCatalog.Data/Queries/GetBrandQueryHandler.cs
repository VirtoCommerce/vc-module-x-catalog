using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using static VirtoCommerce.XCatalog.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetBrandQueryHandler : IRequestHandler<GetBrandQuery, BrandAggregate>
{
    private readonly IBrandStoreSettingSearchService _brandStoreSettingSearchService;
    private readonly ICategoryService _categoryService;
    private readonly IStoreService _storeService;
    private readonly ICatalogService _catalogService;

    public GetBrandQueryHandler(
               IBrandStoreSettingSearchService brandStoreSettingSearchService,
               ICategoryService categoryService,
               IStoreService storeService,
               ICatalogService catalogService)
    {
        _brandStoreSettingSearchService = brandStoreSettingSearchService;
        _categoryService = categoryService;
        _storeService = storeService;
        _catalogService = catalogService;
    }

    public async Task<BrandAggregate> Handle(GetBrandQuery request, CancellationToken cancellationToken)
    {
        var brandStoreSettings = await GetBrandStoreSetting(request.StoreId);
        if (brandStoreSettings == null)
        {
            return null;
        }

        var brandPropertyName = brandStoreSettings.BrandPropertyName ?? DefaultBrandPropertyName;

        var store = await GetStoreAsync(request.StoreId);
        var brandsCatalog = await GetBrandsCatalog(brandStoreSettings.BrandCatalogId);

        var brand = new BrandAggregate
        {
            Store = store,
            Catalog = brandsCatalog,
            BrandPropertyName = brandPropertyName,
        };

        // check for category
        var brandCategory = await _categoryService.GetNoCloneAsync(request.Id);
        if (brandCategory != null)
        {
            brand.Id = brandCategory.Id;
            brand.Name = brandCategory.Name;
            brand.Descriptions = brandCategory.Descriptions?.ToList();
            brand.SeoInfos = brandCategory.SeoInfos?.ToList();
            brand.Properties = brandCategory.Properties?.ToList();
        }
        else
        {
            // fallback
            brand.Id = request.Id;
            brand.Name = request.Id;
        }

        return brand;
    }

    protected virtual async Task<BrandStoreSetting> GetBrandStoreSetting(string storeId)
    {
        var criteria = AbstractTypeFactory<BrandStoreSettingSearchCriteria>.TryCreateInstance();
        criteria.StoreId = storeId;
        criteria.Take = 1;

        var brandStoreSetting = await _brandStoreSettingSearchService.SearchAsync(criteria);
        return brandStoreSetting.Results.FirstOrDefault();
    }

    protected virtual async Task<Store> GetStoreAsync(string storeId)
    {
        return await _storeService.GetNoCloneAsync(storeId);
    }

    protected virtual async Task<Catalog> GetBrandsCatalog(string brandCatalogId)
    {
        return await _catalogService.GetNoCloneAsync(brandCatalogId);
    }
}
