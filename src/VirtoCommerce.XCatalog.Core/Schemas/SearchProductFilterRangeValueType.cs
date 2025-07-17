using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.Xapi.Core.Schemas.ScalarTypes;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas;

public class SearchProductFilterRangeValueType : ExtendableGraphType<SearchProductFilterRangeValue>
{
    public SearchProductFilterRangeValueType()
    {
        Name = "SearchProductFilterRangeValue";
        Description = "Represents a range value in a product search filter";

        Field<AnyValueGraphType>("lower")
            .Description("The starting value of the range")
            .Resolve(x => x.Source.Lower);

        Field<AnyValueGraphType>("upper")
            .Description("The ending value of the range")
            .Resolve(x => x.Source.Upper);

        Field(x => x.IncludeLowerBound).Description("Indicates if the starting bound is included in the range");
        Field(x => x.IncludeUpperBound).Description("Indicates if the ending bound is included in the range");
    }
}
