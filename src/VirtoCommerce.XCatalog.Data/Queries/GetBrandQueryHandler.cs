using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
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
    private readonly ICategorySearchService _categorySearchService;

    public GetBrandQueryHandler(
               IBrandSettingService brandSettingService,
               ICategoryService categoryService,
               IStoreService storeService,
               ICatalogService catalogService,
               ICategorySearchService categorySearchService)
    {
        _brandSettingService = brandSettingService;
        _categoryService = categoryService;
        _storeService = storeService;
        _catalogService = catalogService;
        _categorySearchService = categorySearchService;
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
        var brandCategory = await GetBrandCategory(request, brandsCatalog);
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

    private async Task<Category> GetBrandCategory(GetBrandQuery request, Catalog brandsCatalog)
    {
        var result = default(Category);

        if (!request.Id.IsNullOrEmpty())
        {
            result = await _categoryService.GetNoCloneAsync(request.Id);
        }
        else if (!request.Name.IsNullOrEmpty())
        {
            // If Id is not specified, then the brand will be resolved by Name.
            var categorySearchCriteria = AbstractTypeFactory<CategorySearchCriteria>.TryCreateInstance();
            categorySearchCriteria.CatalogId = brandsCatalog.Id;
            categorySearchCriteria.Keyword = request.Name;
            categorySearchCriteria.ResponseGroup = CategoryResponseGroup.Full.ToString();

            var categorySearchResult = await _categorySearchService.SearchAsync(categorySearchCriteria);
            if (!categorySearchResult.Results.IsNullOrEmpty())
            {
                result = categorySearchResult.Results.FirstOrDefault(x => x.Name.EqualsIgnoreCase(request.Name));
            }
        }

        return result;
    }
}
