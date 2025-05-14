using System.Linq;
using GraphQL.Types;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Extensions;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class BrandType : ExtendableGraphType<BrandAggregate>
    {
        public BrandType()
        {
            Field(x => x.Id, nullable: false).Description("Brand ID.");
            Field(x => x.Name, true).Description("Brand name.");
            Field(x => x.BrandPropertyName, true).Description("Brand property name.");

            Field<BooleanGraphType>("featured")
                .Description("Indicates if the brand is featured.")
                .Resolve(context =>
                {
                    var featured = context.Source.Properties
                        ?.FirstOrDefault(x => x.Name.EqualsIgnoreCase("Featured"))
                        ?.Values
                        ?.FirstOrDefault(x => x.Value != null)
                        ?.Value;

                    return featured ?? false;
                });

            Field<StringGraphType>("description")
                .Arguments(new QueryArguments(new QueryArgument<StringGraphType> { Name = "type" }))
                .Resolve(context =>
                {
                    var descriptions = context.Source.Descriptions;
                    var type = context.GetArgumentOrValue<string>("type");
                    var cultureName = context.GetArgumentOrValue<string>("cultureName");

                    if (!descriptions.IsNullOrEmpty())
                    {
                        var result = descriptions.Where(x => x.DescriptionType.EqualsIgnoreCase(type ?? "FullReview")).FirstBestMatchForLanguage(cultureName) as CategoryDescription
                            ?? descriptions.FirstBestMatchForLanguage(cultureName) as CategoryDescription;

                        return result.Content;
                    }

                    return null;
                });

            ExtendableField<NonNullGraphType<StringGraphType>>("permalink", resolve: context =>
            {
                var source = context.Source;
                var cultureName = context.GetArgumentOrValue<string>("cultureName");

                SeoInfo seoInfo = null;

                if (!source.SeoInfos.IsNullOrEmpty())
                {
                    var store = source.Store;
                    seoInfo = source.SeoInfos.GetBestMatchingSeoInfo(store, cultureName);
                }

                var result = seoInfo ?? SeoInfosExtensions.GetFallbackSeoInfo(source.Id, source.Name, cultureName);

                return $"{source.Catalog.Name}/{result.SemanticUrl}";
            }, description: "Request related SEO info");

            ExtendableField<StringGraphType>("bannerUrl", resolve: context =>
            {
                var result = context.Source.Images.FirstOrDefault(x => x.Group.EqualsIgnoreCase("Banner"))?.Url;
                return result;
            });

            ExtendableField<StringGraphType>("logoUrl", resolve: context =>
            {
                var result = context.Source.Images.FirstOrDefault(x => x.Group.EqualsIgnoreCase("Logo"))?.Url;
                return result;
            });
        }
    }
}
