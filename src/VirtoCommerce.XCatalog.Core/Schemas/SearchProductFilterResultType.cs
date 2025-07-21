using GraphQL.Types;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas;

public class SearchProductFilterResultType : ExtendableGraphType<SearchProductFilterResult>
{
    public SearchProductFilterResultType()
    {
        Name = "SearchProductFilterResult";
        Description = "Represents a filter result for product search";

        Field(x => x.Name, nullable: false).Description("The name of the filter");
        Field(x => x.FilterType, nullable: false).Description("The type of the filter, e.g., 'term' or 'range'");

        Field<StringGraphType>("label")
            .Description("Localized name of the filter (if available)")
            .Resolve(context => context.Source.Label.IsNullOrEmpty()
                ? context.Source.Name
                : context.Source.Label);

        Field<ListGraphType<SearchProductFilterValueType>>("termValues")
            .Description("List of term values in the filter")
            .Resolve(context => context.Source.TermValues);

        Field<ListGraphType<SearchProductFilterRangeValueType>>("rangeValues")
            .Description("List of range values in the filter")
            .Resolve(context => context.Source.RangeValues);
    }
}
