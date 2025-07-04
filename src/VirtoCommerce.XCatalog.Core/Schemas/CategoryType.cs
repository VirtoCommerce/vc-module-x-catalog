using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Types;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Seo.Core.Models;
using VirtoCommerce.StoreModule.Core.Extensions;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

using SeoExtensions = VirtoCommerce.Seo.Core.Extensions.SeoExtensions;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class CategoryType : ExtendableGraphType<ExpCategory>
    {
        public CategoryType(IMediator mediator, IDataLoaderContextAccessor dataLoader)
        {
            Name = "Category";

            Field(x => x.Id, nullable: false).Description("Id of category.");
            Field(x => x.Category.ImgSrc, nullable: true).Description("The category image.");
            Field(x => x.Category.Code, nullable: false).Description("SKU of category.");
            Field<NonNullGraphType<StringGraphType>>("name").Resolve(context =>
            {
                var cultureName = context.GetArgumentOrValue<string>("cultureName");
                var category = context.Source.Category;
                var localizedName = category.LocalizedName?.GetValue(cultureName);
                if (!string.IsNullOrEmpty(localizedName))
                {
                    return localizedName;
                }
                return category.Name;
            }).Description("The name of the category.");

            Field(x => x.Level, nullable: false).Description("Level in hierarchy");
            Field(x => x.Category.Priority, nullable: false).Description("The category priority.");
            Field(x => x.RelevanceScore, nullable: true).Description("Category relevance score");

            Field<StringGraphType>("outline").ResolveAsync(async context =>
            {
                var outlines = context.Source.Category?.Outlines;
                if (outlines.IsNullOrEmpty())
                {
                    return null;
                }

                var loadRelatedCatalogOutlineQuery = context.GetCatalogQuery<LoadRelatedCatalogOutlineQuery>();
                loadRelatedCatalogOutlineQuery.Outlines = outlines;

                var response = await mediator.Send(loadRelatedCatalogOutlineQuery);
                return response.Outline;
            }).Description(@"All parent categories ids relative to the requested catalog and concatenated with \ . E.g. (1/21/344)");

            Field<StringGraphType>("slug").ResolveAsync(async context =>
            {
                var outlines = context.Source.Category?.Outlines;
                if (outlines.IsNullOrEmpty())
                {
                    return null;
                }

                var loadRelatedSlugPathQuery = context.GetCatalogQuery<LoadRelatedSlugPathQuery>();
                loadRelatedSlugPathQuery.Outlines = outlines;

                var response = await mediator.Send(loadRelatedSlugPathQuery);
                return response.Slug;
            }).Description("Request related slug for category");

            Field(x => x.Category.Path, nullable: true).Description("Category path in to the requested catalog  (all parent categories names concatenated. E.g. (parent1/parent2))");

            ExtendableField<NonNullGraphType<SeoInfoType>>("seoInfo", resolve: context =>
            {
                var source = context.Source;
                var cultureName = context.GetArgumentOrValue<string>("cultureName");

                SeoInfo seoInfo = null;

                if (!source.Category.SeoInfos.IsNullOrEmpty())
                {
                    var store = context.GetArgumentOrValue<Store>("store");
                    seoInfo = source.Category.SeoInfos.GetBestMatchingSeoInfo(store, cultureName);
                }

                return seoInfo ?? SeoExtensions.GetFallbackSeoInfo(source.Id, source.Category.Name, cultureName);
            }, description: "Request related SEO info");

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<CategoryDescriptionType>>>>("descriptions",
                  arguments: new QueryArguments(new QueryArgument<StringGraphType> { Name = "type" }),
                  resolve: context =>
                  {
                      var descriptions = context.Source.Category.Descriptions;
                      var cultureName = context.GetArgumentOrValue<string>("cultureName");
                      var type = context.GetArgumentOrValue<string>("type");
                      if (cultureName != null)
                      {
                          descriptions = descriptions.Where(x => string.IsNullOrEmpty(x.LanguageCode) || x.LanguageCode.EqualsIgnoreCase(cultureName)).ToList();
                      }
                      if (type != null)
                      {
                          descriptions = descriptions.Where(x => x.DescriptionType?.EqualsIgnoreCase(type) ?? true).ToList();
                      }
                      return descriptions;
                  });

            ExtendableField<CategoryDescriptionType>("description",
                arguments: new QueryArguments(new QueryArgument<StringGraphType> { Name = "type" }),
                resolve: context =>
                {
                    var descriptions = context.Source.Category.Descriptions;
                    var type = context.GetArgumentOrValue<string>("type");
                    var cultureName = context.GetArgumentOrValue<string>("cultureName");

                    if (!descriptions.IsNullOrEmpty())
                    {
                        return descriptions.Where(x => x.DescriptionType.EqualsIgnoreCase(type ?? "FullReview")).FirstBestMatchForLanguage(cultureName) as CategoryDescription
                            ?? descriptions.FirstBestMatchForLanguage(cultureName) as CategoryDescription;
                    }

                    return null;
                });

            var parentField = new FieldType
            {
                Name = "parent",
                Type = GraphTypeExtensionHelper.GetActualType<CategoryType>(),
                Resolver = new FuncFieldResolver<ExpCategory, IDataLoaderResult<ExpCategory>>(context =>
                {
                    var loader = dataLoader.Context.GetOrAddBatchLoader<string, ExpCategory>("parentsCategoryLoader", ids => LoadCategoriesAsync(mediator, ids, context));

                    return TryGetCategoryParentId(context, out var parentCategoryId)
                        ? loader.LoadAsync(parentCategoryId)
                        : new DataLoaderResult<ExpCategory>(Task.FromResult<ExpCategory>(null));
                })
            };
            AddField(parentField);

            Field<NonNullGraphType<BooleanGraphType>, bool>("hasParent")
                .Description("Have a parent")
                .Resolve(context => TryGetCategoryParentId(context, out _));

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<OutlineType>>>>("outlines",
                "Outlines",
                resolve: context => context.Source.Category.Outlines ?? Array.Empty<Outline>());

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<ImageType>>>>("images",
                "Images",
                resolve: context => context.Source.Category.Images ?? Array.Empty<Image>());

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<BreadcrumbType>>>>(
                "breadcrumbs",
                "Breadcrumbs",
                resolve: context => context.Source.Category.Outlines.GetBreadcrumbs(context));

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<PropertyType>>>>("properties",
                arguments: new QueryArguments(new QueryArgument<ListGraphType<StringGraphType>> { Name = "names" }),
                resolve: context =>
            {
                var names = context.GetArgument<string[]>("names");
                var cultureName = context.GetValue<string>("cultureName");
                var result = context.Source.Category.Properties.ExpandByValues(cultureName);
                if (!names.IsNullOrEmpty())
                {
                    result = result.Where(x => names.Contains(x.Name, StringComparer.InvariantCultureIgnoreCase)).ToList();
                }
                return result;
            });

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<CategoryType>>>>(
                nameof(ExpCategory.ChildCategories),
                resolve: context => context.Source.ChildCategories ?? Array.Empty<ExpCategory>());
        }

        protected virtual bool TryGetCategoryParentId(IResolveFieldContext<ExpCategory> context, out string parentId)
        {
            parentId = null;
            var outlines = context.Source.Category?.Outlines;
            if (outlines.IsNullOrEmpty())
            {
                return false;
            }

            var store = context.GetArgumentOrValue<Store>("store");

            foreach (var outline in outlines.Where(outline => outline.Items.Any(x => x.Id.Equals(store.Catalog))))
            {
                parentId = outline.Items.Take(outline.Items.Count - 1).Select(x => x.Id).LastOrDefault();

                //parentId should be a category id, not a catalog id
                if (parentId != null && parentId != store.Catalog)
                {
                    return true;
                }
            }
            return false;
        }

        private static async Task<IDictionary<string, ExpCategory>> LoadCategoriesAsync(IMediator mediator, IEnumerable<string> ids, IResolveFieldContext context)
        {
            var loadCategoryQuery = context.GetCatalogQuery<LoadCategoryQuery>();
            loadCategoryQuery.ObjectIds = ids.Where(x => x != null).ToArray();
            loadCategoryQuery.IncludeFields = context.SubFields.Values.GetAllNodesPaths(context).ToArray();

            var response = await mediator.Send(loadCategoryQuery);
            return response.Categories.ToDictionary(x => x.Id);
        }
    }
}
