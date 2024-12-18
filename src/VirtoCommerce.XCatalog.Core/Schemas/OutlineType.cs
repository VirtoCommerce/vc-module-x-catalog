using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Outlines;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class OutlineType : ExtendableGraphType<Outline>
    {
        public OutlineType()
        {
            Field<ListGraphType<NonNullGraphType<OutlineItemType>>>("items",
                "Outline items",
                resolve: context => context.Source.Items);
        }
    }
}
