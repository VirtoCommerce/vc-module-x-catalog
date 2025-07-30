using System;
using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas;

public class SearchProductFilterRangeValueType : ExtendableGraphType<SearchProductFilterRangeValue>
{
    public SearchProductFilterRangeValueType()
    {
        Name = "SearchProductFilterRangeValue";
        Description = "Represents a range value in a product search filter";

        Field<StringGraphType>("lower")
            .Description("The starting value of the range")
            .Resolve(x => GetValue(x.Source.Lower));

        Field<StringGraphType>("upper")
            .Description("The ending value of the range")
            .Resolve(x => GetValue(x.Source.Upper));

        Field(x => x.IncludeLowerBound).Description("Indicates if the starting bound is included in the range");
        Field(x => x.IncludeUpperBound).Description("Indicates if the ending bound is included in the range");
    }

    private static string GetValue(object value)
    {
        return value is DateTime dateTime ? dateTime.ToString("O") : value?.ToString();
    }
}
