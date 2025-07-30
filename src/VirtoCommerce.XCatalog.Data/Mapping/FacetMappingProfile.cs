using System;
using System.Globalization;
using System.Linq;
using AutoMapper;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Models.Facets;
using CoreFacets = VirtoCommerce.Xapi.Core.Models.Facets;

namespace VirtoCommerce.XCatalog.Data.Mapping
{
    public class FacetMappingProfile : Profile
    {
        public FacetMappingProfile()
        {
            CreateMap<Aggregation, CoreFacets.FacetResult>().IncludeAllDerived().ConvertUsing((request, facet, context) =>
            {
                context.Items.TryGetValue("cultureName", out var cultureNameObj);
                var cultureName = cultureNameObj as string;
                CoreFacets.FacetResult result = request.AggregationType switch
                {
                    "attr" => new CoreFacets.TermFacetResult
                    {
                        Terms = request.Items?.Select(x => new CoreFacets.FacetTerm
                        {
                            Count = x.Count,
                            IsSelected = x.IsApplied,
                            Term = x.Value?.ToString(),

                            Label = x.Labels?.FirstBestMatchForLanguage(x => x.Language, cultureName)?.Label ?? x.Value.ToString(),
                        })
                            .ToArray() ?? [],
                        Name = request.Field
                    },
                    "range" or "pricerange" => new CoreFacets.RangeFacetResult
                    {
                        Ranges = request.Items?.Select(x => new CoreFacets.FacetRange
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
                }

                return result;
            });
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
}
