using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Extensions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.Xapi.Core.Index;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Data.Extensions;

namespace VirtoCommerce.XCatalog.Data.Index
{
    public class IndexSearchRequestBuilder
    {
        public const string ScoreSortingFieldName = "score";

        public string UserId { get; private set; }
        public string StoreId { get; private set; }
        public string CultureName { get; private set; }
        public string CurrencyCode { get; private set; }

        public IFilter Filter => SearchRequest.Filter;
        public IList<AggregationRequest> Aggregations => SearchRequest.Aggregations;

        protected SearchRequest SearchRequest { get; set; }

        private static readonly string[] _wildcards = ["?", "*"];

        public IndexSearchRequestBuilder()
        {
            SearchRequest = OverridenType<SearchRequest>.New();
            SearchRequest.Filter = new AndFilter
            {
                ChildFilters = new List<IFilter>(),
            };
            SearchRequest.SearchFields = ["__content"];
            SearchRequest.Sorting = [new SortingField(ScoreSortingFieldName, true)];
            SearchRequest.Skip = 0;
            SearchRequest.Take = 20;
            SearchRequest.Aggregations = [];
            SearchRequest.IncludeFields = [];
        }

        public IndexSearchRequestBuilder WithStoreId(string storeId)
        {
            StoreId = storeId;
            return this;
        }

        public IndexSearchRequestBuilder WithUserId(string userId)
        {
            UserId = userId;
            return this;
        }

        public IndexSearchRequestBuilder WithFuzzy(bool fuzzy, int? fuzzyLevel = null)
        {
            SearchRequest.IsFuzzySearch = fuzzy;
            SearchRequest.Fuzziness = fuzzyLevel;
            return this;
        }

        public IndexSearchRequestBuilder WithPaging(int skip, int take)
        {
            SearchRequest.Skip = skip;
            SearchRequest.Take = take;

            return this;
        }

        public IndexSearchRequestBuilder WithSearchPhrase(string searchPhrase)
        {
            SearchRequest.SearchKeywords = searchPhrase;
            return this;
        }

        public IndexSearchRequestBuilder WithCultureName(string cultureName)
        {
            CultureName = cultureName;

            if (!string.IsNullOrEmpty(CultureName))
            {
                SearchRequest.SearchFields.Add($"__content_{cultureName}".ToLowerInvariant());
            }

            return this;
        }

        public IndexSearchRequestBuilder WithIncludeFields(params string[] includeFields)
        {
            SearchRequest.IncludeFields ??= [];

            if (!includeFields.IsNullOrEmpty())
            {
                SearchRequest.IncludeFields.AddRange(includeFields);
            }

            return this;
        }

        public IndexSearchRequestBuilder AddCertainDateFilter(DateTime certainDate)
        {
            var startDateFilter = new RangeFilter
            {
                FieldName = "startdate",
                Values = [new RangeFilterValue
                {
                    Lower = null,
                    Upper = certainDate.ToString("O"),
                    IncludeLower = false,
                    IncludeUpper = true,
                }]
            };

            var endDateFilter = new RangeFilter
            {
                FieldName = "enddate",
                Values = [new RangeFilterValue
                {
                    Lower = certainDate.ToString("O"),
                    Upper = null,
                    IncludeLower = false,
                    IncludeUpper = true,
                }]
            };

            AddFiltersToSearchRequest([startDateFilter, endDateFilter]);

            return this;
        }

        public IndexSearchRequestBuilder AddObjectIds(IEnumerable<string> ids)
        {
            var values = ids as IList<string> ?? ids?.ToArray();
            if (!values.IsNullOrEmpty())
            {
                AddFiltersToSearchRequest([new IdsFilter { Values = values }]);
                SearchRequest.Take = values.Count;
            }

            return this;
        }

        public IndexSearchRequestBuilder AddTerms(IEnumerable<string> terms)
        {
            if (terms != null)
            {
                var termsFields = GetFiltersFromTerm(terms);
                AddFiltersToSearchRequest(termsFields);
            }

            return this;
        }

        public IndexSearchRequestBuilder AddTerms(IEnumerable<string> terms, bool skipIfExists)
        {
            if (terms != null)
            {
                var termsFields = GetFiltersFromTerm(terms);
                AddFiltersToSearchRequest(termsFields, skipIfExists);
            }

            return this;
        }

        public IndexSearchRequestBuilder ParseFilters(ISearchPhraseParser phraseParser, string filterPhrase)
        {
            ArgumentNullException.ThrowIfNull(phraseParser);

            if (string.IsNullOrEmpty(filterPhrase))
            {
                return this;
            }

            var parseResult = phraseParser.Parse(filterPhrase);

            var filters = new List<IFilter>();

            foreach (var filter in parseResult.Filters)
            {
                FilterSyntaxMapper.MapFilterAdditionalSyntax(filter);

                var convertedFilter = ConvertFilter(filter);

                filters.Add(convertedFilter);
            }

            AddFiltersToSearchRequest(filters.ToArray());

            return this;
        }

        private IFilter ConvertFilter(IFilter filter)
        {
            var result = filter;

            switch (filter)
            {
                case TermFilter termFilter:
                    {
                        var wildcardValues = termFilter.Values.Where(x => _wildcards.Any(x.Contains)).ToArray();

                        if (wildcardValues.Length != 0)
                        {
                            var orFilter = new OrFilter
                            {
                                ChildFilters = new List<IFilter>()
                            };

                            var wildcardTermFilters = wildcardValues.Select(x => new WildCardTermFilter
                            {
                                FieldName = termFilter.FieldName,
                                Value = x
                            }).ToList();

                            orFilter.ChildFilters.AddRange(wildcardTermFilters);

                            termFilter.Values = termFilter.Values.Except(wildcardValues).ToList();

                            if (termFilter.Values.Any())
                            {
                                orFilter.ChildFilters.Add(termFilter);
                            }

                            // return OrFilter with added termFilters instead
                            result = orFilter;
                        }
                        break;
                    }

                case RangeFilter rangeFilter:
                    if (rangeFilter.FieldName.EqualsIgnoreCase("price"))
                    {
                        rangeFilter.FieldName = $"price_{CurrencyCode}".ToLowerInvariant();
                    }
                    break;
            }

            return result;
        }

        public IndexSearchRequestBuilder ParseFacets(ISearchPhraseParser phraseParser,
            string facetPhrase,
            IList<AggregationRequest> predefinedAggregations = null)
        {
            ArgumentNullException.ThrowIfNull(phraseParser);

            SearchRequest.Aggregations = predefinedAggregations ?? [];

            if (string.IsNullOrEmpty(facetPhrase))
            {
                return this;
            }

            // PT-1613: Support aliases for Facet expressions e.g price.usd[TO 200) as price_below_200
            // PT-1613: Need to create a new  Antlr file with g4-lexer rules and generate parser especially for facets expression
            // that will return proper AggregationRequests objects
            var parseResult = phraseParser.Parse(facetPhrase);

            //Term facets
            if (!string.IsNullOrEmpty(parseResult.Keyword))
            {
                parseResult.Keyword = parseResult.Keyword.AddLanguageSpecificFacets(CultureName);

                var termFacetExpressions = parseResult.Keyword.Split(" ");
                parseResult.Filters.AddRange(termFacetExpressions.Select(x => new TermFilter
                {
                    FieldName = x,
                    Values = new List<string>()
                }));
            }

            SearchRequest.Aggregations = parseResult.Filters
                .Select<IFilter, AggregationRequest>(filter =>
                {
                    FilterSyntaxMapper.MapFilterAdditionalSyntax(filter);

                    return filter switch
                    {
                        RangeFilter rangeFilter => new RangeAggregationRequest
                        {
                            Id = filter.Stringify(true),
                            FieldName = rangeFilter.FieldName,
                            Values = rangeFilter.Values.Select(x => new RangeAggregationRequestValue
                            {
                                Id = x.Stringify(),
                                Lower = x.Lower,
                                Upper = x.Upper,
                                IncludeLower = x.IncludeLower,
                                IncludeUpper = x.IncludeUpper
                            }).ToList()
                        },
                        TermFilter termFilter => new TermAggregationRequest
                        {
                            FieldName = termFilter.FieldName,
                            Id = filter.Stringify(),
                            Size = 0
                        },
                        _ => null,
                    };
                })
                .Where(x => x != null)
                .ToList();

            return this;
        }

        public IndexSearchRequestBuilder WithCurrency(string currencyCode)
        {
            CurrencyCode = currencyCode;
            return this;
        }

        public IndexSearchRequestBuilder AddSorting(string sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                return this;
            }

            var sortFields = new List<SortingField>();

            foreach (var sortInfo in SortInfo.Parse(sort))
            {
                var sortingField = new SortingField();
                if (sortInfo is GeoSortInfo geoSortInfo)
                {
                    sortingField = new GeoDistanceSortingField
                    {
                        Location = geoSortInfo.GeoPoint
                    };
                }
                sortingField.FieldName = sortInfo.SortColumn.ToLowerInvariant();
                sortingField.IsDescending = sortInfo.SortDirection == SortDirection.Descending;

                switch (sortingField.FieldName)
                {
                    case "name":
                    case "title":
                        sortFields.Add(new SortingField("name", sortingField.IsDescending));
                        break;
                    case "price" when !string.IsNullOrEmpty(CurrencyCode):
                        sortFields.Add(new SortingField($"price_{CurrencyCode}".ToLowerInvariant(), sortingField.IsDescending));
                        break;

                    default:
                        sortFields.Add(sortingField);
                        break;
                }
            }

            if (sortFields.Count != 0)
            {
                SearchRequest.Sorting = sortFields;
            }

            return this;
        }

        public IndexSearchRequestBuilder ApplyMultiSelectFacetSearch()
        {
            foreach (var aggr in SearchRequest.Aggregations ?? [])
            {
                var aggregationFilterFieldName = aggr.FieldName ?? (aggr.Filter as INamedFilter)?.FieldName;

                var clonedFilter = (AndFilter)SearchRequest.Filter.Clone();

                // For multi-select facet mechanism, we should select
                // search request filters which do not have the same
                // names such as aggregation filter
                clonedFilter.ChildFilters = clonedFilter
                    .ChildFilters
                    .Where(x =>
                    {
                        var result = true;

                        if (x is INamedFilter namedFilter)
                        {
                            result = !(aggregationFilterFieldName?.StartsWith(namedFilter.FieldName, true, CultureInfo.InvariantCulture) ?? false);
                        }

                        return result;
                    })
                    .ToList();

                aggr.Filter = aggr.Filter == null ? clonedFilter : aggr.Filter.And(clonedFilter);
            }

            return this;
        }

        public virtual SearchRequest Build()
        {
            //Apply multi-select facet search policy by default
            return SearchRequest;
        }

        protected void AddFiltersToSearchRequest(IList<IFilter> filters, bool skipIfExists = false)
        {
            var childFilters = ((AndFilter)SearchRequest.Filter).ChildFilters;

            //Skip adding duplicate filters
            if (skipIfExists)
            {
                var existsFiltersNames = childFilters.OfType<INamedFilter>()
                    .Select(x => x.FieldName)
                    .Distinct()
                    .ToArray();

                var comparer = StringComparer.InvariantCultureIgnoreCase;
                filters = filters
                    .Where(x => !(x is INamedFilter filter && existsFiltersNames.Contains(filter.FieldName, comparer)))
                    .ToArray();
            }

            childFilters.AddRange(filters);
        }

        private static IFilter[] GetFiltersFromTerm(IEnumerable<string> terms)
        {
            const string commaEscapeString = "%x2C";

            var nameValueDelimiter = new[] { ':' };
            var valuesDelimiter = new[] { ',' };

            return terms.Select(item => item.Split(nameValueDelimiter, 2))
                .Where(item => item.Length == 2)
                .Select(item => new TermFilter
                {
                    FieldName = item[0],
                    Values = item[1].Split(valuesDelimiter, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Replace(commaEscapeString, ","))
                        .ToArray()
                }).ToArray<IFilter>();
        }
    }
}
