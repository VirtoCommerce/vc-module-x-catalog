using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Outlines;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class OutlineItemType : ObjectGraphType<OutlineItem>
    {
        public OutlineItemType()
        {
            Field(x => x.Id, nullable: false);
            Field(x => x.Name, nullable: false);
            Field(x => x.SeoObjectType, nullable: false);
            Field<ListGraphType<NonNullGraphType<SeoInfoType>>>("seoInfos",
                "SEO info",
                resolve: context => context.Source.SeoInfos);
        }
    }
}
