using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using static VirtoCommerce.XCatalog.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetBrandQueryHandler : IRequestHandler<GetBrandQuery, BrandAggregate>
{
    private readonly IBrandSettingService _brandSettingService;
    private readonly ICategoryService _categoryService;
    private readonly IStoreService _storeService;
    private readonly ICatalogService _catalogService;

    public GetBrandQueryHandler(
               IBrandSettingService brandSettingService,
               ICategoryService categoryService,
               IStoreService storeService,
               ICatalogService catalogService)
    {
        _brandSettingService = brandSettingService;
        _categoryService = categoryService;
        _storeService = storeService;
        _catalogService = catalogService;
    }

    public async Task<BrandAggregate> Handle(GetBrandQuery request, CancellationToken cancellationToken)
    {
        var brandStoreSettings = await _brandSettingService.GetByStoreIdAsync(request.StoreId);
        if (brandStoreSettings?.BrandCatalogId == null)
        {
            return null;
        }

        var brandPropertyName = brandStoreSettings.BrandPropertyName ?? DefaultBrandPropertyName;

        var store = await _storeService.GetNoCloneAsync(request.StoreId);
        var brandsCatalog = await _catalogService.GetNoCloneAsync(brandStoreSettings.BrandCatalogId);

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
            brand.LocalizedName = brandCategory.LocalizedName;
            brand.Descriptions = brandCategory.Descriptions;
            brand.SeoInfos = brandCategory.SeoInfos;
            brand.Properties = brandCategory.Properties;
            brand.Images = brandCategory.Images;
        }
        else
        {
            // fallback
            brand.Id = request.Id;
            brand.Name = request.Id;
        }

        return brand;
    }
}
