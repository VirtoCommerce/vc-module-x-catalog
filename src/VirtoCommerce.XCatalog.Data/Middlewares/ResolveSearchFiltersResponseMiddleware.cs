using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Index;

namespace VirtoCommerce.XCatalog.Data.Middlewares;

public class ResolveSearchFiltersResponseMiddleware(
    IPropertyService propertyService,
    ISearchPhraseParser phraseParser,
    IPropertyDictionaryItemSearchService propertyDictionaryItemSearchService,
    ICategoryService categoryService)
    : IAsyncMiddleware<SearchProductResponse>
{
    public virtual async Task Run(SearchProductResponse parameter, Func<SearchProductResponse, Task> next)
    {
        var responseGroup = EnumUtility.SafeParse(parameter.Query.GetResponseGroup(), ExpProductResponseGroup.None);
        if (responseGroup.HasFlag(ExpProductResponseGroup.ParseFilters))
        {
            parameter.Filters = CreateFilters(parameter.Query);
        }

        if (parameter.Filters.IsNullOrEmpty())
        {
            await next(parameter);
            return;
        }

        var propertyTermFilters = parameter.Filters.Where(x => x.FilterType == "term" && (x.Name != "__outline" && x.Name != "__outline_named")).ToList();

        var cultureName = parameter.Query.CultureName ?? parameter.Store.DefaultLanguage;

        if (propertyTermFilters.Count > 0)
        {
            // try resolve terms names via product properties
            ResolveTermLabelsByProductProperties(parameter, propertyTermFilters, cultureName);

            // try resolve terms names via property metadata service
            await ResolveTermLabelsByPropertyMetadataAsync(parameter, propertyTermFilters, cultureName);

            // try resolve term values names via property items
            await ResolveTermItemsLabelsAsync(propertyTermFilters, cultureName);
        }

        var outlineTermFilters = parameter.Filters.Where(x => x.Name == "__outline_named" || x.Name == "__outline").ToList();
        if (outlineTermFilters.Count > 0)
        {
            await ResolveTermLabelsByCategoryAsync(outlineTermFilters, cultureName);
        }

        await next(parameter);
    }


    private List<SearchProductFilterResult> CreateFilters(SearchProductQuery request)
    {
        var userSearchRequestContainer = new IndexSearchRequestBuilder()
            .ParseFilters(phraseParser, request.Filter)
            .Build();

        return userSearchRequestContainer
            .GetChildFilters()
            .Select(f => new SearchProductFilterResult
            {
                Name = f.GetFieldName(),
                FilterType = f is TermFilter ? "term" : "range",
                TermValues = f is TermFilter termFilter ? termFilter.Values.Select(x => new SearchProductFilterTermValue { Value = x }).ToList() : null,
                RangeValues = f is RangeFilter rangeFilter ? rangeFilter.Values.Select(x => new SearchProductFilterRangeValue
                {
                    Lower = x.Lower,
                    Upper = x.Upper,
                    IncludeLowerBound = x.IncludeLower,
                    IncludeUpperBound = x.IncludeUpper
                }).ToList() : null
            })
            .ToList();
    }

    private static void ResolveTermLabelsByProductProperties(SearchProductResponse parameter, List<SearchProductFilterResult> termFilters, string cultureName)
    {
        var productProperties = parameter.Results
            .Where(x => x.IndexedProduct is not null && x.IndexedProduct.Properties is not null)
            .SelectMany(x => x.IndexedProduct.Properties)
            .Where(property => property?.Id is not null)
            .ToArray();

        foreach (var termFilter in termFilters)
        {
            var productProperty = productProperties.FirstOrDefault(x => x.Name.EqualsIgnoreCase(termFilter.Name));
            if (productProperty == null)
            {
                continue;
            }

            termFilter.PropertyId = productProperty.Id;

            var displayName = productProperty.DisplayNames?.FirstOrDefault(x => x.LanguageCode.EqualsIgnoreCase(cultureName));
            if (displayName != null)
            {
                termFilter.Label = displayName.Name;
            }
        }
    }

    private async Task ResolveTermLabelsByPropertyMetadataAsync(SearchProductResponse parameter, List<SearchProductFilterResult> termFilters, string cultureName)
    {
        var termFiltersToLocalize = termFilters.Where(x => x.Label == null).ToList();
        if (termFiltersToLocalize.Count == 0)
        {
            return;
        }

        var allProperties = await propertyService.GetAllCatalogPropertiesAsync(parameter.Store.Catalog);

        foreach (var termFilter in termFiltersToLocalize)
        {
            var property = allProperties.FirstOrDefault(x => x.Name.EqualsIgnoreCase(termFilter.Name));
            if (property != null)
            {
                termFilter.PropertyId = property.Id;

                var displayName = property.DisplayNames?.FirstOrDefault(x => x.LanguageCode.EqualsIgnoreCase(cultureName));
                if (displayName != null)
                {
                    termFilter.Label = displayName.Name;
                }
            }
        }
    }

    private async Task ResolveTermItemsLabelsAsync(List<SearchProductFilterResult> termFilters, string cultureName)
    {
        var propertyIds = termFilters
            .Where(x => x.PropertyId != null)
            .Select(x => x.PropertyId)
            .Distinct()
            .ToArray();

        var dictionaryItemsSearchResult = await GetPropertiesItemsAsync(propertyIds);

        foreach (var termFilter in termFilters)
        {
            if (termFilter.PropertyId == null)
            {
                continue;
            }

            foreach (var termFitlerValue in termFilter.TermValues)
            {
                var propertyItem = dictionaryItemsSearchResult.FirstOrDefault(x => x.PropertyId == termFilter.PropertyId && x.Alias.EqualsIgnoreCase(termFitlerValue.Value));
                if (propertyItem == null)
                {
                    continue;
                }

                var localizedValue = propertyItem.LocalizedValues
                    .FirstOrDefault(x => x.LanguageCode.EqualsIgnoreCase(cultureName));

                termFitlerValue.Label = localizedValue?.Value ?? propertyItem.Alias;
            }
        }
    }

    private async Task<List<PropertyDictionaryItem>> GetPropertiesItemsAsync(string[] propertyIds)
    {
        var result = new List<PropertyDictionaryItem>();
        var criteria = new PropertyDictionaryItemSearchCriteria
        {
            PropertyIds = propertyIds
        };

        int totalCount;

        do
        {
            var dictionaryItemsSearchResult = await propertyDictionaryItemSearchService.SearchNoCloneAsync(criteria);
            result.AddRange(dictionaryItemsSearchResult.Results);

            totalCount = dictionaryItemsSearchResult.TotalCount;
            criteria.Skip += criteria.Take;
        }
        while (criteria.Skip < totalCount);

        return result;
    }

    private async Task ResolveTermLabelsByCategoryAsync(List<SearchProductFilterResult> namedOutlineFilters, string cultureName)
    {
        var categoryIds = namedOutlineFilters
            .SelectMany(x => x.TermValues)
            .Select(x => GetOutlineId(x.Value))
            .Where(x => x != null)
            .Distinct()
            .ToArray();

        var categries = await categoryService.GetNoCloneAsync(categoryIds);

        foreach (var termFilter in namedOutlineFilters)
        {
            foreach (var termValue in termFilter.TermValues)
            {
                var categoryId = GetOutlineId(termValue.Value);
                if (categoryId == null)
                {
                    continue;
                }

                var category = categries.FirstOrDefault(x => x.Id == categoryId);
                if (category == null)
                {
                    continue;
                }

                var localizedName = category.LocalizedName?.GetValue(cultureName);
                termValue.Label = localizedName ?? category.Name;
            }
        }
    }

    // from catalog module
    private static string GetOutlineId(string outlineValue)
    {
        if (outlineValue.IsNullOrEmpty())
        {
            return null;
        }

        // Outline structure: catalog/category1/.../categoryN/current-category-id___current-category-name
        var outlineParts = outlineValue.Split("/", StringSplitOptions.RemoveEmptyEntries);
        var namedOutline = outlineParts[^1];

        var namedOutlineParts = namedOutline.Split("___", StringSplitOptions.RemoveEmptyEntries);
        return namedOutlineParts.Length == 2 ? namedOutlineParts[0] : namedOutline;
    }
}
