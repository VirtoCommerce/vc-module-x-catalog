using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Index;

namespace VirtoCommerce.XCatalog.Data.Middlewares;

public class ResolveSearchFiltersResponseMiddleware(IPropertyService propertyService, ISearchPhraseParser phraseParser) : IAsyncMiddleware<SearchProductResponse>
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

        var termFilters = parameter.Filters.Where(x => x.FilterType == "term" && x.Name != "__outline").ToList();
        if (termFilters.Count <= 0)
        {
            await next(parameter);
            return;
        }

        var cultureName = parameter.Query.CultureName ?? parameter.Store.DefaultLanguage;

        // try resolve terms names via product properties
        ResolveTermLabelsByProductProperties(parameter, termFilters, cultureName);

        // try resolve terms names via property metadata service
        await ResolveTermLabelsByPropertyMetadata(parameter, termFilters, cultureName);

        await next(parameter);
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
            var displayName = productProperty?.DisplayNames?.FirstOrDefault(x => x.LanguageCode.EqualsIgnoreCase(cultureName));
            if (displayName != null)
            {
                termFilter.Label = displayName.Name;
            }
        }
    }

    private async Task ResolveTermLabelsByPropertyMetadata(SearchProductResponse parameter, List<SearchProductFilterResult> termFilters, string cultureName)
    {
        var allProperties = await propertyService.GetAllCatalogPropertiesAsync(parameter.Store.Catalog);

        foreach (var termFilter in termFilters.Where(x => x.Label == null))
        {
            var property = allProperties.FirstOrDefault(x => x.Name.EqualsIgnoreCase(termFilter.Name));
            if (property != null)
            {
                var displayName = property.DisplayNames?.FirstOrDefault(x => x.LanguageCode.EqualsIgnoreCase(cultureName));
                if (displayName != null)
                {
                    termFilter.Label = displayName.Name;
                }
            }
        }
    }

    private List<SearchProductFilterResult> CreateFilters(SearchProductQuery request)
    {
        var userSearchRequestContairer = new IndexSearchRequestBuilder()
            .ParseFilters(phraseParser, request.Filter)
            .Build();

        return userSearchRequestContairer
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
}
