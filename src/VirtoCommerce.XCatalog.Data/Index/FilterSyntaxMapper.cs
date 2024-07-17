using System.Collections.Generic;
using System.Text.RegularExpressions;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.XCatalog.Data.Index
{
    public static class FilterSyntaxMapper
    {
        private abstract class FilterToIndexMapper
        {
            protected FilterToIndexMapper()
            {
            }

            public virtual bool CanMap(IFilter filter)
            {
                return false;
            }

            public virtual IFilter Map(IFilter filter)
            {
                return filter;
            }

            protected static string GetFilterName(IFilter filter)
            {
                string result = null;
                if (filter is TermFilter termFilter)
                {
                    result = termFilter.FieldName;
                }
                else if (filter is RangeFilter rangeFilter)
                {
                    result = rangeFilter.FieldName;
                }
                return result;
            }

            protected static IFilter SetFilterName(IFilter filter, string filterName)
            {
                if (filter is TermFilter termFilter)
                {
                    termFilter.FieldName = filterName;
                }
                else if (filter is RangeFilter rangeFilter)
                {
                    rangeFilter.FieldName = filterName;
                }
                return filter;
            }
        }

        private sealed class RegexpNameMapper : FilterToIndexMapper
        {
            private readonly Regex _filterPattern;
            private readonly string _namePattern;

            public RegexpNameMapper(Regex filterPattern, string namePattern)
            {
                _filterPattern = filterPattern;
                _namePattern = namePattern;
            }

            public override bool CanMap(IFilter filter)
            {
                var filterName = GetFilterName(filter);
                var result = filterName != null;
                if (result)
                {
                    result = _filterPattern.Match(filterName).Success;
                }
                return result;
            }

            public override IFilter Map(IFilter filter)
            {
                var newFilterName = _filterPattern.Replace(GetFilterName(filter), _namePattern);
                return SetFilterName(filter, newFilterName);
            }
        }

        private static IList<FilterToIndexMapper> _allMappers = new List<FilterToIndexMapper>()
        {
            new RegexpNameMapper(new Regex(@"price.([A-Za-z]{3})", RegexOptions.Compiled | RegexOptions.IgnoreCase), "price_$1"),
            new RegexpNameMapper(new Regex(@"catalog.id", RegexOptions.Compiled | RegexOptions.IgnoreCase), "catalog"),
            new RegexpNameMapper(new Regex(@"category.path", RegexOptions.Compiled | RegexOptions.IgnoreCase), "__path"),
            new RegexpNameMapper(new Regex(@"category.subtree", RegexOptions.Compiled | RegexOptions.IgnoreCase), "__outline"),
            new RegexpNameMapper(new Regex(@"categories.subtree", RegexOptions.Compiled | RegexOptions.IgnoreCase), "__outline"),
            new RegexpNameMapper(new Regex(@"sku", RegexOptions.Compiled | RegexOptions.IgnoreCase), "code"),
            new RegexpNameMapper(new Regex(@"properties.([A-Za-z0-9_\s+])", RegexOptions.Compiled | RegexOptions.IgnoreCase), "$1")
        };

        public static IFilter MapFilterAdditionalSyntax(IFilter filter)
        {
            foreach (var mapper in _allMappers)
            {
                if (mapper.CanMap(filter))
                {
                    return mapper.Map(filter);
                }
            }
            return filter;
        }
    }
}
