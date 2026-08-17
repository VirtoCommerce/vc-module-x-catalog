using System;
using System.Globalization;
using System.Linq;
using AutoMapper;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Models.Facets;
using CatalogModuleConstants = VirtoCommerce.CatalogModule.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Tests.Mappers;

public class LegacyFacetMappingProfile : Profile
{
    public LegacyFacetMappingProfile()
    {
        CreateMap<Aggregation, FacetResult>().IncludeAllDerived().ConvertUsing((request, facet, context) =>
        {
            context.Items.TryGetValue("cultureName", out var cultureNameObj);
            var cultureName = cultureNameObj as string;
            FacetResult result = request.AggregationType switch
            {
                "attr" => new TermFacetResult
                {
                    Terms = request.Items?.Select(x => new FacetTerm
                    {
                        Count = x.Count,
                        IsSelected = x.IsApplied,
                        Term = x.Value?.ToString(),

                        Label = x.Labels?.FirstBestMatchForLanguage(x => x.Language, cultureName)?.Label ?? x.Value.ToString(),
                    })
                        .ToArray() ?? [],
                    Name = request.Field
                },
                "range" or "pricerange" => new RangeFacetResult
                {
                    Ranges = request.Items?.Select(x => new FacetRange
                    {
                        Count = x.Count,
                        From = ToNullableDecimal(x.RequestedLowerBound),
                        IncludeFrom = x.IncludeLower,
                        FromStr = x.RequestedLowerBound,
                        To = ToNullableDecimal(x.RequestedUpperBound),
                        IncludeTo = x.IncludeUpper,
                        ToStr = x.RequestedUpperBound,
                        IsSelected = x.IsApplied,
                        Label = x.Value?.ToString(),
                    })
                        .ToArray() ?? [],
                    Name = request.Field,
                    Statistics = request.Statistics == null ? null : new RangeFacetStatistics
                    {
                        Max = request.Statistics.Max,
                        Min = request.Statistics.Min,
                    }
                },
                _ => null
            };

            if (result != null)
            {
                result.Label = request.Labels?.FirstBestMatchForLanguage(x => x.Language, cultureName)?.Label ?? result.Name;

                context.Items.TryGetValue("order", out var orderObj);
                if (orderObj != null)
                {
                    result.Order = (int)orderObj;
                }

                SortTermFacetResultByLabels(request, result);
            }

            return result;
        });
    }

    private static void SortTermFacetResultByLabels(Aggregation request, FacetResult result)
    {
        if (result is not TermFacetResult termFacetResult || termFacetResult.Terms.IsNullOrEmpty())
        {
            return;
        }

        if (request.TermValuesSortingType.IsNullOrEmpty() || request.TermValuesSortingType.EqualsIgnoreCase(CatalogModuleConstants.TermValuesSortingTypeNameAscending))
        {
            termFacetResult.Terms = termFacetResult.Terms.OrderBy(x => x.Label).ToArray();
        }
        else if (request.TermValuesSortingType.EqualsIgnoreCase(CatalogModuleConstants.TermValuesSortingTypeNameDescending))
        {
            termFacetResult.Terms = termFacetResult.Terms.OrderByDescending(x => x.Label).ToArray();
        }
    }

    private static decimal? ToNullableDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }
}
