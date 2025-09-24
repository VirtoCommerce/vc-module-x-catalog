using System.Linq;
using GraphQL.Types;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Seo.Core.Models;
using VirtoCommerce.StoreModule.Core.Extensions;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;

using SeoExtensions = VirtoCommerce.Seo.Core.Extensions.SeoExtensions;

namespace VirtoCommerce.XCatalog.Core.Schemas;

public class BrandType : ExtendableGraphType<BrandAggregate>
{
    public BrandType()
    {
        Field(x => x.Id, nullable: false).Description("Brand ID.");
        Field(x => x.BrandPropertyName, true).Description("Brand property name.");
        Field<StringGraphType>("brandPropertyValue")
            .Resolve(context => context.Source.Name)
            .Description("Unlocalized brand name.");

        Field<StringGraphType>("name")
            .Resolve(context =>
            {
                var cultureName = context.GetArgumentOrValue<string>("cultureName") ?? context.Source.Store?.DefaultLanguage;
                if (cultureName == null)
                {
                    return context.Source.Name;
                }

                var localizedName = context.Source.LocalizedName?.GetValue(cultureName);
                if (string.IsNullOrEmpty(localizedName))
                {
                    return context.Source.Name;
                }

                return localizedName;
            }).Description("Brand name.");

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
                if (context.Source.Descriptions.IsNullOrEmpty())
                {
                    return null;
                }

                var cultureName = context.GetArgumentOrValue<string>("cultureName") ?? context.Source.Store?.DefaultLanguage;
                if (cultureName == null)
                {
                    return null;
                }

                var type = context.GetArgumentOrValue<string>("type");
                var descriptions = context.Source.Descriptions;

                var result = descriptions.Where(x => x.DescriptionType.EqualsIgnoreCase(type ?? "FullReview")).FirstBestMatchForLanguage(cultureName) as CategoryDescription
                        ?? descriptions.FirstBestMatchForLanguage(cultureName) as CategoryDescription;

                return result?.Content;
            });

        Field<NonNullGraphType<StringGraphType>>("permalink")
            .Resolve(context =>
            {
                var source = context.Source;
                var store = source.Store;
                var cultureName = context.GetArgumentOrValue<string>("cultureName") ?? context.Source.Store?.DefaultLanguage;

                SeoInfo categorySeoInfo = null;
                if (!source.SeoInfos.IsNullOrEmpty())
                {
                    categorySeoInfo = source.SeoInfos.GetBestMatchingSeoInfo(store, cultureName);
                }

                SeoInfo catalogSeoInfo = null;
                if (!source.Catalog.SeoInfos.IsNullOrEmpty())
                {
                    catalogSeoInfo = source.Catalog.SeoInfos.GetBestMatchingSeoInfo(store, cultureName);
                }

                var brandSeoInfo = categorySeoInfo ?? SeoExtensions.GetFallbackSeoInfo(source.Id, source.Name, cultureName);
                var catalogSemanticUrl = catalogSeoInfo?.SemanticUrl ?? source.Catalog.Name;
                return $"{catalogSemanticUrl}/{brandSeoInfo.SemanticUrl}";
            });

        Field<StringGraphType>("bannerUrl")
            .Resolve(context =>
            {
                var result = context.Source.Images.FirstOrDefault(x => x.Group.EqualsIgnoreCase("Banner"))?.Url;
                return result;
            });

        Field<StringGraphType>("logoUrl")
            .Resolve(context =>
            {
                var result = context.Source.Images.FirstOrDefault(x => x.Group.EqualsIgnoreCase("Logo"))?.Url;
                return result;
            });
    }
}
