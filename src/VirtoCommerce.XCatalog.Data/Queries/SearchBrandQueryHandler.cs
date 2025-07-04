using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using static VirtoCommerce.XCatalog.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class SearchBrandQueryHandler : IRequestHandler<SearchBrandQuery, SearchBrandResponse>
{
    private readonly IBrandSettingService _brandSettingService;
    private readonly ICategoryService _categoryService;
    private readonly ICategoryTreeService _categoryTreeService;
    private readonly IStoreService _storeService;
    private readonly IMediator _mediator;

    private readonly CategoryResponseGroup _defaultResponseGroup =
        CategoryResponseGroup.Info |
        CategoryResponseGroup.WithImages |
        CategoryResponseGroup.WithProperties |
        CategoryResponseGroup.WithSeo |
        CategoryResponseGroup.WithDescriptions;

    public SearchBrandQueryHandler(
        IBrandSettingService brandSettingService,
        ICategoryService categoryService,
        ICategoryTreeService categoryTreeService,
        IMediator mediator,
        IStoreService storeService)
    {
        _brandSettingService = brandSettingService;
        _categoryService = categoryService;
        _categoryTreeService = categoryTreeService;
        _mediator = mediator;
        _storeService = storeService;
    }

    public virtual async Task<SearchBrandResponse> Handle(SearchBrandQuery request, CancellationToken cancellationToken)
    {
        var result = new SearchBrandResponse();

        // get brands settings (catalog and property)
        var brandStoreSettings = await _brandSettingService.GetByStoreIdAsync(request.StoreId);
        if (brandStoreSettings == null || brandStoreSettings.BrandCatalogId == null)
        {
            return result;
        }

        var brandPropertyName = brandStoreSettings.BrandPropertyName ?? DefaultBrandPropertyName;

        var store = await _storeService.GetNoCloneAsync(request.StoreId);

        // get all brands terms
        var brandNames = await GetProductBrandNames(request, brandPropertyName);

        // get categories 
        var brandCategories = await GetBrandCategories(brandStoreSettings.BrandCatalogId);

        var brandsCatalog = brandCategories.FirstOrDefault()?.Catalog;

        // compare the two
        var brands = CreateBrandsByCategories(brandNames, brandCategories, brandsCatalog, store, brandPropertyName, request.CultureName);

        result.TotalCount = brands.Count;

        // paging and sorting and keyword search on brands
        result.Results = PrepareResult(brands, request);

        return result;
    }

    protected virtual IList<BrandAggregate> PrepareResult(IList<BrandAggregate> brands, SearchBrandQuery request)
    {
        var queryable = brands.AsQueryable();

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            queryable = queryable.Where(x => x.Name.Contains(request.Keyword, StringComparison.InvariantCulture));
        }

        var sortInfos = new List<SortInfo> { new() { SortColumn = nameof(BrandAggregate.Id) } };
        if (!string.IsNullOrEmpty(request.Sort))
        {
            sortInfos = SortInfo.Parse(request.Sort).ToList();
        }

        var results = queryable
            .OrderBySortInfos(sortInfos)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToList();

        return results;
    }

    protected virtual async Task<IList<Category>> GetBrandCategories(string brandCatalogId)
    {
        IList<Category> result = new List<Category>();

        var treeNodes = await _categoryTreeService.GetNodesWithChildren(brandCatalogId, new List<string> { null }, false);
        var firstLevelCategoryIds = treeNodes?.FirstOrDefault()?.ChildIds ?? [];
        if (firstLevelCategoryIds.Count > 0)
        {
            result = await _categoryService.GetNoCloneAsync(firstLevelCategoryIds, _defaultResponseGroup.ToString());
        }

        return result;
    }

    protected virtual IList<BrandAggregate> CreateBrandsByCategories(IList<string> brandNames, IList<Category> brandCategories, Catalog brandsCatalog, Store store, string brandPropertyName, string cultureName)
    {
        var brands = new List<BrandAggregate>();

        foreach (var brandName in brandNames)
        {
            var brandCategory = brandCategories.FirstOrDefault(x => x.Name.EqualsIgnoreCase(brandName));

            var brand = new BrandAggregate
            {
                Store = store,
                Catalog = brandsCatalog,
                BrandPropertyName = brandPropertyName,
            };

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
                // fallback brand
                brand.Id = brandName;
                brand.Name = brandName;
            }

            brands.Add(brand);
        }

        return brands;
    }

    protected virtual async Task<IList<string>> GetProductBrandNames(SearchBrandQuery request, string brandPropertyName)
    {
        var result = new List<string>();

        var productsRequest = AbstractTypeFactory<SearchProductQuery>.TryCreateInstance();
        productsRequest.StoreId = request?.StoreId;
        productsRequest.CultureName = request?.CultureName;
        productsRequest.CurrencyCode = request?.CurrencyCode;
        productsRequest.UserId = request?.UserId ?? ModuleConstants.AnonymousUser.UserName;
        productsRequest.Take = 0;
        productsRequest.Facet = brandPropertyName;
        productsRequest.IncludeFields =
        [
            "term_facets.name",
            "term_facets.label",
            "term_facets.terms.label",
            "term_facets.terms.term",
            "term_facets.terms.count",
        ];

        var productsResult = await _mediator.Send(productsRequest);

        var facet = productsResult.Facets.OfType<TermFacetResult>().FirstOrDefault(x => x.Name.EqualsIgnoreCase(brandPropertyName));

        if (facet != null)
        {
            foreach (var term in facet.Terms)
            {
                result.Add(term.Term);
            }
        }

        if (request != null && !request.BrandNames.IsNullOrEmpty())
        {
            result = result.Intersect(request.BrandNames, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return result;
    }
}
