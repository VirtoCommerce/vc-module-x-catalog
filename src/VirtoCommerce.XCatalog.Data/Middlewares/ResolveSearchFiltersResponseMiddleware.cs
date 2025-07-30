using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Data.Middlewares;

public class ResolveSearchFiltersResponseMiddleware(
    IPropertyService propertyService,
    IPropertyDictionaryItemSearchService propertyDictionaryItemSearchService,
    ICategoryService categoryService)
    : IAsyncMiddleware<SearchProductResponse>
{
    private const string _termFilterType = "term";
    private const string _rangeFilterType = "range";

    public virtual async Task Run(SearchProductResponse parameter, Func<SearchProductResponse, Task> next)
    {
        var responseGroup = EnumUtility.SafeParse(parameter.Query.GetResponseGroup(), ExpProductResponseGroup.None);

        if (responseGroup.HasFlag(ExpProductResponseGroup.ParseFilters))
        {
            parameter.Filters = GetFilters(parameter);
        }

        if (parameter.Filters?.Count > 0)
        {
            await ResolveTermFilterLabels(parameter);
        }

        await next(parameter);
    }


    private static List<SearchProductFilterResult> GetFilters(SearchProductResponse response)
    {
        var userFilters = ConvertFilters(response.UserFilters, isGenerated: false);
        var generatedFilters = ConvertFilters(response.GeneratedFilters, isGenerated: true);

        return userFilters.Concat(generatedFilters).ToList();
    }

    private static IEnumerable<SearchProductFilterResult> ConvertFilters(IList<IFilter> filters, bool isGenerated)
    {
        foreach (var filter in filters)
        {
            var result = new SearchProductFilterResult
            {
                Name = filter.GetFieldName(),
                IsGenerated = isGenerated,
            };

            switch (filter)
            {
                case TermFilter termFilter:
                    result.FilterType = _termFilterType;
                    result.TermValues = termFilter.Values.Select(x => new SearchProductFilterTermValue { Value = x }).ToList();
                    break;
                case RangeFilter rangeFilter:
                    result.FilterType = _rangeFilterType;
                    result.RangeValues = rangeFilter.Values
                        .Select(x => new SearchProductFilterRangeValue
                        {
                            Lower = x.Lower,
                            Upper = x.Upper,
                            IncludeLowerBound = x.IncludeLower,
                            IncludeUpperBound = x.IncludeUpper,
                        })
                        .ToList();
                    break;
            }

            yield return result;
        }
    }

    private async Task ResolveTermFilterLabels(SearchProductResponse response)
    {
        var cultureName = response.Query.CultureName ?? response.Store.DefaultLanguage;

        var propertyTermFilters = response.Filters
            .Where(x => x.FilterType == _termFilterType && !IsOutlineFilter(x))
            .ToList();

        if (propertyTermFilters.Count > 0)
        {
            // Try to resolve term names via product properties
            ResolveTermLabelsByProductProperties(propertyTermFilters, cultureName, response.Results);

            // Try to resolve term names via property metadata service
            await ResolveTermLabelsByPropertyMetadataAsync(propertyTermFilters, cultureName, response.Store.Catalog);

            // Try to resolve term value labels via property values
            await ResolveTermValueLabelsAsync(propertyTermFilters, cultureName);
        }

        var outlineFilters = response.Filters
            .Where(IsOutlineFilter)
            .ToList();

        if (outlineFilters.Count > 0)
        {
            await ResolveTermLabelsByCategoryAsync(outlineFilters, cultureName);
        }
    }

    private static bool IsOutlineFilter(SearchProductFilterResult filter)
    {
        return filter.Name is "__outline" or "__outline_named";
    }

    private static void ResolveTermLabelsByProductProperties(List<SearchProductFilterResult> termFilters, string cultureName, IList<ExpProduct> products)
    {
        var productProperties = products
            .Where(x => x.IndexedProduct?.Properties != null)
            .SelectMany(x => x.IndexedProduct.Properties)
            .Where(property => property?.Id != null)
            .ToArray();

        foreach (var filter in termFilters)
        {
            var productProperty = productProperties.FirstOrDefault(x => x.Name.EqualsIgnoreCase(filter.Name));
            if (productProperty == null)
            {
                continue;
            }

            filter.PropertyId = productProperty.Id;

            var displayName = productProperty.DisplayNames?.FirstOrDefault(x => x.LanguageCode.EqualsIgnoreCase(cultureName));
            if (displayName != null)
            {
                filter.Label = displayName.Name;
            }
        }
    }

    private async Task ResolveTermLabelsByPropertyMetadataAsync(List<SearchProductFilterResult> termFilters, string cultureName, string catalogId)
    {
        var filtersToLocalize = termFilters.Where(x => x.Label == null).ToList();
        if (filtersToLocalize.Count == 0)
        {
            return;
        }

        var allCatalogProperties = await propertyService.GetAllCatalogPropertiesAsync(catalogId);
        var allProperties = allCatalogProperties as IList<Property> ?? allCatalogProperties.ToList();

        foreach (var filter in filtersToLocalize)
        {
            var property = allProperties.FirstOrDefault(x => x.Name.EqualsIgnoreCase(filter.Name));
            if (property != null)
            {
                filter.PropertyId = property.Id;

                var displayName = property.DisplayNames?.FirstOrDefault(x => x.LanguageCode.EqualsIgnoreCase(cultureName));
                if (displayName != null)
                {
                    filter.Label = displayName.Name;
                }
            }
        }
    }

    private async Task ResolveTermValueLabelsAsync(List<SearchProductFilterResult> termFilters, string cultureName)
    {
        var propertyIds = termFilters
            .Where(x => x.PropertyId != null)
            .Select(x => x.PropertyId)
            .Distinct()
            .ToArray();

        var dictionaryItems = await GetPropertyDictionaryItemsAsync(propertyIds);

        foreach (var filter in termFilters)
        {
            if (filter.PropertyId == null)
            {
                continue;
            }

            foreach (var termFilterValue in filter.TermValues)
            {
                var dictionaryItem = dictionaryItems.FirstOrDefault(x => x.PropertyId == filter.PropertyId && x.Alias.EqualsIgnoreCase(termFilterValue.Value));
                if (dictionaryItem == null)
                {
                    continue;
                }

                var localizedValue = dictionaryItem.LocalizedValues.FirstOrDefault(x => x.LanguageCode.EqualsIgnoreCase(cultureName));
                termFilterValue.Label = localizedValue?.Value ?? dictionaryItem.Alias;
            }
        }
    }

    private async Task<List<PropertyDictionaryItem>> GetPropertyDictionaryItemsAsync(string[] propertyIds)
    {
        var result = new List<PropertyDictionaryItem>();
        var criteria = new PropertyDictionaryItemSearchCriteria
        {
            PropertyIds = propertyIds,
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

    private async Task ResolveTermLabelsByCategoryAsync(List<SearchProductFilterResult> outlineFilters, string cultureName)
    {
        var categoryIds = outlineFilters
            .SelectMany(x => x.TermValues)
            .Select(x => OutlineString.GetLastItemId(x.Value))
            .Where(x => x != null)
            .Distinct()
            .ToArray();

        var categories = await categoryService.GetNoCloneAsync(categoryIds);

        foreach (var filter in outlineFilters)
        {
            foreach (var termValue in filter.TermValues)
            {
                var categoryId = OutlineString.GetLastItemId(termValue.Value);
                if (categoryId == null)
                {
                    continue;
                }

                var category = categories.FirstOrDefault(x => x.Id == categoryId);
                if (category == null)
                {
                    continue;
                }

                var localizedName = category.LocalizedName?.GetValue(cultureName);
                termValue.Label = localizedName ?? category.Name;
            }
        }
    }
}
