using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas;

public class ChildCategoriesQueryResponseType : ExtendableGraphType<ChildCategoriesQueryResponse>
{
    public ChildCategoriesQueryResponseType()
    {
        ExtendableField<ListGraphType<CategoryType>>(
            nameof(ChildCategoriesQueryResponse.ChildCategories),
            resolve: context => context.Source.ChildCategories);
    }
}
