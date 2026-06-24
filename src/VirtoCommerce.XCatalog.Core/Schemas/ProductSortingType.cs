using GraphQL.Types;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class ProductSortingType : ObjectGraphType<ProductSorting>
    {
        public ProductSortingType()
        {
            Field<NonNullGraphType<StringGraphType>>("id")
                .Description("Stable sort code, used as the ?sort=<id> value.")
                .Resolve(context => context.Source.Id);

            Field<StringGraphType>("name")
                .Description("Localized display name.")
                .Resolve(context => context.Source.Name);

            Field<NonNullGraphType<BooleanGraphType>>("isDefault")
                .Description("Whether this is the store default ordering.")
                .Resolve(context => context.Source.IsDefault);

            Field<NonNullGraphType<BooleanGraphType>>("selected")
                .Description("Whether this ordering is applied to the current result.")
                .Resolve(context => context.Source.IsSelected);
        }
    }
}
