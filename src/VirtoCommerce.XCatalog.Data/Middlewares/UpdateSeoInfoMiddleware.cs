using System;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Models;
using static VirtoCommerce.XCatalog.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    public class UpdateSeoInfoMiddleware : IAsyncMiddleware<PipelineSeoInfoRequest>
    {
        private readonly IBrandStoreSettingService _brandStoreSettingService;
        private readonly ICategoryService _categoryService;
        private readonly ICatalogService _catalogService;


        public UpdateSeoInfoMiddleware(
            IBrandStoreSettingService brandStoreSettingService,
            ICategoryService categoryService,
            ICatalogService catalogService)
        {
            _brandStoreSettingService = brandStoreSettingService;
            _categoryService = categoryService;
            _catalogService = catalogService;
        }

        public virtual async Task Run(PipelineSeoInfoRequest parameter, Func<PipelineSeoInfoRequest, Task> next)
        {
            var permalink = parameter.SeoSearchCriteria?.Permalink.TrimStart('/') ?? string.Empty;

            // return Brands seo immediately if slug is "brands"
            if (permalink.EqualsIgnoreCase("brands"))
            {
                parameter.SeoInfo = CreateSeoInfo(BrandsSeoType, parameter.SeoSearchCriteria, null);
                await next(parameter);
                return;
            }

            if (parameter.SeoInfo == null || parameter.SeoInfo?.ObjectType == nameof(Category))
            {
                var brandStoreSettings = await _brandStoreSettingService.GetByStoreIdAsync(parameter.SeoSearchCriteria.StoreId);
                if (brandStoreSettings != null || brandStoreSettings.BrandCatalogId == null)
                {
                    parameter.SeoInfo = await CreateBrandSeoInfoAsync(brandStoreSettings, parameter, permalink);
                }
            }

            await next(parameter);
        }

        private async Task<SeoInfo> CreateBrandSeoInfoAsync(BrandStoreSetting brandStoreSettings, PipelineSeoInfoRequest parameter, string permalink)
        {
            var seoInfo = parameter.SeoInfo;

            var catalog = await _catalogService.GetNoCloneAsync(brandStoreSettings.BrandCatalogId);
            if (!IsBrandCatalogQuery(catalog, permalink))
            {
                return seoInfo;
            }

            var isExistingBrandSeo = false;
            if (seoInfo != null)
            {
                var category = await _categoryService.GetNoCloneAsync(parameter.SeoInfo.ObjectId);
                isExistingBrandSeo = category.CatalogId == brandStoreSettings.BrandCatalogId;
            }

            seoInfo = CreateSeoInfo(BrandSeoType, parameter.SeoSearchCriteria, isExistingBrandSeo ? seoInfo : null);
            return seoInfo;
        }

        private static bool IsBrandCatalogQuery(Catalog catalog, string permalink)
        {
            var segments = permalink.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            return catalog != null && segments.First().EqualsIgnoreCase(catalog.Name);
        }

        protected virtual SeoInfo CreateSeoInfo(string seoType, SeoSearchCriteria criteria, SeoInfo existingBrandSeo)
        {
            var seoInfo = AbstractTypeFactory<SeoInfo>.TryCreateInstance();

            if (existingBrandSeo != null)
            {
                seoInfo = existingBrandSeo.CloneTyped();
            }
            else
            {
                seoInfo.Id = criteria.Slug;
                seoInfo.SemanticUrl = criteria.Slug;
                seoInfo.ObjectId = criteria.Slug;
                seoInfo.LanguageCode = criteria.LanguageCode;
            }

            seoInfo.ObjectType = seoType;
            return seoInfo;
        }
    }
}
