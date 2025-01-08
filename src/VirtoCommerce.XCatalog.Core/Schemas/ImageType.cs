using GraphQL.Types;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class ImageType : ExtendableGraphType<Image>
    {
        /// <summary>
        ///
        /// </summary>
        /// <example>
        /// "sortOrder":0,
        /// "binaryData":null,
        /// "relativeUrl":"catalog/LG55EG9600/1431446771000_1119832.jpg",
        /// "url":"http://localhost:10645/assets/catalog/LG55EG9600/1431446771000_1119832.jpg",
        /// "typeId":"Image",
        /// "group":"images",
        /// "name":"1431446771000_1119832.jpg",
        /// "outerId":null,
        /// "languageCode":null,
        /// "isInherited":false,
        /// "seoObjectType":"Image",
        /// "seoInfos":null,
        /// "id":"a40b05e231ba4be0893bd4bbcfb92376"
        /// </example>
        public ImageType()
        {
            Field<NonNullGraphType<StringGraphType>>("id")
                .Description("The unique ID of the image")
                .Resolve(context => context.Source.Id);
            Field<StringGraphType>("name")
                .Description("The name of the image")
                .Resolve(context => context.Source.Name);
            Field<StringGraphType>("group")
                .Description("The group of the image")
                .Resolve(context => context.Source.Group);
            Field<NonNullGraphType<StringGraphType>>("url")
                .Description("The URL of the image")
                .Resolve(context => context.Source.Url);
            Field<StringGraphType>("relativeUrl")
                .Description("The relative URL of the image")
                .Resolve(context => context.Source.RelativeUrl);
            Field<NonNullGraphType<IntGraphType>>("sortOrder")
                .Description("Sort order")
                .Resolve(context => context.Source.SortOrder);
            Field<StringGraphType>("cultureName")
                .Description("Culture name")
                .Resolve(context => context.Source.LanguageCode);
            Field<StringGraphType>("description")
                .Description("The description of the image")
                .Resolve(context => context.Source.Description);
        }
    }
}
