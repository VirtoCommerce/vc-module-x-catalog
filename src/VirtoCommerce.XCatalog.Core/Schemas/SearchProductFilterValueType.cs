using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas;

public class SearchProductFilterValueType : ExtendableGraphType<SearchProductFilterTermValue>
{
    public SearchProductFilterValueType()
    {
        Name = "SearchProductFilterValue";
        Description = "Represents a term value in a product search filter";

        Field(x => x.Value).Description("The value of the term in the filter");
        Field(x => x.Label, nullable: true).Description("The label of the term in the filter, used for display purposes");
    }
}
