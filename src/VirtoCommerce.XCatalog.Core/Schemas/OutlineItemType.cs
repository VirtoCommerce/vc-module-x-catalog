using GraphQL.Types;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.Xapi.Core.Schemas;
using SeoInfoType = VirtoCommerce.Seo.ExperienceApi.Schemas.SeoInfoType;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class OutlineItemType : ExtendableGraphType<OutlineItem>
    {
        public OutlineItemType()
        {
            Field(x => x.Id, nullable: false);
            Field(x => x.Name, nullable: false);
            Field(x => x.SeoObjectType, nullable: false);
            Field<ListGraphType<NonNullGraphType<SeoInfoType>>>("seoInfos")
                .Description("SEO info")
                .Resolve(context => context.Source.SeoInfos);
        }
    }
}
