using System;
using System.Linq;
using AutoMapper;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.Xapi.Core.Extensions;
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
                    "pricerange" => new CoreFacets.RangeFacetResult
                    {
                        Ranges = request.Items?.Select(x => new CoreFacets.FacetRange
                        {
                            Count = x.Count,
                            From = Convert.ToInt64(x.RequestedLowerBound),
                            IncludeFrom = x.IncludeLower,
                            FromStr = x.RequestedLowerBound,
                            To = Convert.ToInt64(x.RequestedUpperBound),
                            IncludeTo = x.IncludeUpper,
                            ToStr = x.RequestedUpperBound,
                            IsSelected = x.IsApplied,
                            Label = x.Value?.ToString(),
                        })
                            .ToArray() ?? [],
                        Name = request.Field,
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
    }
}
