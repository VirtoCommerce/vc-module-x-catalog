using GraphQL.Types;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class AssetType : ExtendableGraphType<Asset>
    {
        public AssetType()
        {
            Name = "Asset";

            Field(x => x.Id, nullable: false).Description("The unique ID of the asset.");
            Field(x => x.Name, nullable: true).Description("The name of the asset.");
            Field(x => x.MimeType, nullable: true).Description("The MIME type of the asset.");
            Field(x => x.Size, nullable: false).Description("The size of the asset in bytes.");
            Field(x => x.Url, nullable: false).Description("The URL of the asset.");
            Field(x => x.RelativeUrl, nullable: true).Description("The relative URL of the asset.");
            Field(x => x.TypeId, nullable: false).Description("The type ID of the asset.");
            Field(x => x.Group, nullable: true).Description("The group of the asset.");
            Field(x => x.Description, nullable: true).Description("The description of the asset.");
            Field<StringGraphType>("cultureName")
                .Description("Culture name")
                .Resolve(context => context.Source.LanguageCode);
        }
    }
}
