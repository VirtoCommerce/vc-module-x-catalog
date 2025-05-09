using System;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
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
        private readonly ICategoryService _categoryService;
        private readonly ICatalogService _catalogService;
        private readonly IBrandStoreSettingSearchService _brandStoreSettingSearchService;
        private readonly IMediator _mediator;

        public UpdateSeoInfoMiddleware(
            ICategoryService categoryService,
            IBrandStoreSettingSearchService brandStoreSettingSearchService,
            IMediator mediator,
            ICatalogService catalogService)
        {
            _categoryService = categoryService;
            _brandStoreSettingSearchService = brandStoreSettingSearchService;
            _mediator = mediator;
            _catalogService = catalogService;
        }

        public virtual async Task Run(PipelineSeoInfoRequest parameter, Func<PipelineSeoInfoRequest, Task> next)
        {
            //if (parameter.SeoInfo != null && parameter.SeoInfo.ObjectType == nameof(Category))
            //{
            //    var brandStoreSettings = await GetBrandStoreSetting(parameter.SeoSearchCriteria.StoreId);
            //    if (brandStoreSettings != null)
            //    {
            //        var category = await _categoryService.GetNoCloneAsync(parameter.SeoInfo.ObjectId);

            //        if (category.CatalogId == brandStoreSettings.BrandCatalogId)
            //        {
            //            parameter.SeoInfo.ObjectType = BrandSeoType;
            //            await next(parameter);
            //        }
            //    }
            //}

            if (parameter.SeoInfo == null || parameter.SeoInfo?.ObjectType == nameof(Category))
            {
                var brandStoreSettings = await GetBrandStoreSetting(parameter.SeoSearchCriteria.StoreId);
                if (brandStoreSettings != null)
                {
                    var catalog = await _catalogService.GetNoCloneAsync(brandStoreSettings.BrandCatalogId);

                    var permalink = parameter.SeoSearchCriteria?.Permalink.TrimStart('/');
                    var slug = parameter.SeoSearchCriteria?.Slug.TrimStart('/');

                    if (catalog != null &&
                        (permalink?.StartsWith(catalog.Name, StringComparison.OrdinalIgnoreCase) == true ||
                         slug?.StartsWith(catalog.Name, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        if (parameter.SeoInfo?.ObjectType == nameof(Category))
                        {
                            var category = await _categoryService.GetNoCloneAsync(parameter.SeoInfo.ObjectId);
                            if (category.CatalogId == brandStoreSettings.BrandCatalogId)
                            {
                                parameter.SeoInfo.ObjectType = BrandSeoType;
                                await next(parameter);
                                return;
                            }
                        }

                        parameter.SeoInfo = AbstractTypeFactory<SeoInfo>.TryCreateInstance();
                        parameter.SeoInfo.ObjectType = BrandSeoType;
                        parameter.SeoInfo.Id = parameter.SeoSearchCriteria.Slug;
                        parameter.SeoInfo.SemanticUrl = parameter.SeoSearchCriteria.Slug;
                        parameter.SeoInfo.ObjectId = parameter.SeoSearchCriteria.Slug;
                    }
                }
            }

            await next(parameter);
        }

        protected virtual async Task<BrandStoreSetting> GetBrandStoreSetting(string storeId)
        {
            var criteria = AbstractTypeFactory<BrandStoreSettingSearchCriteria>.TryCreateInstance();
            criteria.StoreId = storeId;
            criteria.Take = 1;

            var brandStoreSetting = await _brandStoreSettingSearchService.SearchAsync(criteria);
            return brandStoreSetting.Results.FirstOrDefault();
        }
    }
}
