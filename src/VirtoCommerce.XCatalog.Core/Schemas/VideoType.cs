using GraphQL.Types;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class VideoType : ExtendableGraphType<Video>
    {
        public VideoType()
        {
            Field(d => d.Name, nullable: false).Description("Video name");
            Field(d => d.Description, nullable: false).Description("Video description");
            Field(d => d.UploadDate, nullable: true).Description("Video upload date");
            Field(d => d.ThumbnailUrl, nullable: false).Description("Video thumbnail URL");
            Field(d => d.ContentUrl, nullable: false).Description("Video URL");
            Field(d => d.EmbedUrl, nullable: true).Description("Embedded video URL");
            Field(d => d.Duration, nullable: true).Description("Video duration");
            Field<StringGraphType>("cultureName").Description("Culture name").Resolve(context => context.Source.LanguageCode);
            Field(d => d.OwnerId, nullable: false).Description("ID of the object video is attached to");
            Field(d => d.OwnerType, nullable: false).Description("Type of the object video is attached to (Product, Category)");
            Field(d => d.SortOrder, nullable: false).Description("Sort order");
        }
    }
}
