using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Builders;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Types;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Seo.Core.Models;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using static VirtoCommerce.Xapi.Core.ModuleConstants;
using SeoExtensions = VirtoCommerce.Seo.Core.Extensions.SeoExtensions;
using SeoInfoType = VirtoCommerce.Seo.ExperienceApi.Schemas.SeoInfoType;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class ProductType : ExtendableGraphType<ExpProduct>
    {
        /// <example>
        ///{
        ///    product(id: "f1b26974b7634abaa0900e575a99476f")
        ///    {
        ///        id
        ///        code
        ///        category{ id code name hasParent slug }
        ///        name
        ///        metaTitle
        ///        metaDescription
        ///        metaKeywords
        ///        brandName
        ///        slug
        ///        imgSrc
        ///        productType
        ///        masterVariation {
        ///        images{ id url name }
        ///        assets{ id size url }
        ///        prices(cultureName: "en-us"){
        ///            list { amount }
        ///            currency
        ///        }
        ///        availabilityData{
        ///            availableQuantity
        ///            inventories{
        ///                inStockQuantity
        ///                fulfillmentCenterId
        ///                fulfillmentCenterName
        ///                allowPreorder
        ///                allowBackorder
        ///            }
        ///        }
        ///        properties{ id name valueType value valueId }
        ///    }
        ///}
        /// </example>
        public ProductType(IMediator mediator, IDataLoaderContextAccessor dataLoader)
        {
            Name = "Product";
            Description = "Products are the sellable goods in an e-commerce project.";

            Field(d => d.IndexedProduct.Id, nullable: false).Description("The unique ID of the product.");
            Field(d => d.IndexedProduct.Code, nullable: false).Description("The product SKU.");
            Field<StringGraphType>("catalogId")
                .Description("The unique ID of the catalog")
                .Resolve(context => context.Source.IndexedProduct.CatalogId);
            Field(d => d.IndexedProduct.ProductType, nullable: true).Description("The type of product");
            Field(d => d.IndexedProduct.MinQuantity, nullable: true).Description("Min. quantity");
            Field(d => d.IndexedProduct.MaxQuantity, nullable: true).Description("Max. quantity");
            Field(d => d.IndexedProduct.PackSize, nullable: false).Description("Defines the number of items in a package. Quantity step for your product's.");
            Field(d => d.RelevanceScore, nullable: true).Description("Product relevance score");

            var productField = new FieldType
            {
                Name = "isConfigurable",
                Type = typeof(NonNullGraphType<BooleanGraphType>),
                Description = "Product is configurable",
                Resolver = new FuncFieldResolver<ExpProduct, IDataLoaderResult<bool>>(context =>
                {
                    var loader = dataLoader.Context.GetOrAddBatchLoader<string, bool>("products_active_configurations", async (ids) =>
                    {
                        var query = new GetProductConfigurationsQuery
                        {
                            ProductIds = ids.ToArray()
                        };

                        return await mediator.Send(query);
                    });
                    return loader.LoadAsync(context.Source.Id);
                })
            };
            AddField(productField);


            Field<StringGraphType>("outline").ResolveAsync(async context =>
            {
                var outlines = context.Source.IndexedProduct.Outlines;
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
                var outlines = context.Source.IndexedProduct.Outlines;
                if (outlines.IsNullOrEmpty())
                {
                    return null;
                }

                var loadRelatedSlugPathQuery = context.GetCatalogQuery<LoadRelatedSlugPathQuery>();
                loadRelatedSlugPathQuery.Outlines = outlines;

                var response = await mediator.Send(loadRelatedSlugPathQuery);
                return response.Slug;
            }).Description("Request related slug for product");

            Field<NonNullGraphType<StringGraphType>>("name").Resolve(context =>
            {
                var cultureName = context.GetArgumentOrValue<string>("cultureName");
                var product = context.Source.IndexedProduct;
                var localizedName = product.LocalizedName?.GetValue(cultureName);
                if (!string.IsNullOrEmpty(localizedName))
                {
                    return localizedName;
                }
                return product.Name;
            }).Description("The name of the product.");

            ExtendableField<NonNullGraphType<SeoInfoType>>("seoInfo", resolve: context =>
            {
                var source = context.Source;
                var cultureName = context.GetArgumentOrValue<string>("cultureName");

                SeoInfo seoInfo = null;

                if (!source.IndexedProduct.SeoInfos.IsNullOrEmpty())
                {
                    var store = context.GetArgumentOrValue<Store>("store");
                    seoInfo = SeoExtensions.GetBestMatchingSeoInfo(source.IndexedProduct.SeoInfos, store.Id, store.DefaultLanguage, cultureName);
                }

                return seoInfo ?? SeoExtensions.GetFallbackSeoInfo(source.Id, source.IndexedProduct.Name, cultureName);
            }, description: "Request related SEO info");

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<DescriptionType>>>>("descriptions",
                  arguments: new QueryArguments(new QueryArgument<StringGraphType> { Name = "type" }),
                  resolve: context =>
                {
                    var reviews = context.Source.IndexedProduct.Reviews;
                    var cultureName = context.GetArgumentOrValue<string>("cultureName");
                    var type = context.GetArgumentOrValue<string>("type");
                    if (cultureName != null)
                    {
                        reviews = reviews.Where(x => string.IsNullOrEmpty(x.LanguageCode) || x.LanguageCode.EqualsIgnoreCase(cultureName)).ToList();
                    }
                    if (type != null)
                    {
                        reviews = reviews.Where(x => x.ReviewType?.EqualsIgnoreCase(type) ?? true).ToList();
                    }
                    return reviews;
                });

            ExtendableField<DescriptionType>("description",
                arguments: new QueryArguments(new QueryArgument<StringGraphType> { Name = "type" }),
                resolve: context =>
            {
                var reviews = context.Source.IndexedProduct.Reviews;
                var type = context.GetArgumentOrValue<string>("type");
                var cultureName = context.GetArgumentOrValue<string>("cultureName");

                if (!reviews.IsNullOrEmpty())
                {
                    return reviews.Where(x => x.ReviewType.EqualsIgnoreCase(type ?? "FullReview")).FirstBestMatchForLanguage(cultureName) as EditorialReview
                        ?? reviews.FirstBestMatchForLanguage(cultureName) as EditorialReview;
                }

                return null;
            });

            ExtendableFieldAsync<CategoryType>(
                "category",
                resolve: async context =>
                {
                    var categoryId = context.Source.IndexedProduct.CategoryId;

                    var loadCategoryQuery = context.GetCatalogQuery<LoadCategoryQuery>();
                    loadCategoryQuery.ObjectIds = [categoryId];
                    loadCategoryQuery.IncludeFields = context.SubFields.Values.GetAllNodesPaths(context).ToArray();

                    var response = await mediator.Send(loadCategoryQuery);

                    return response.Categories.FirstOrDefault();
                });

            Field<StringGraphType>("imgSrc")
                .Description("The product main image URL.")
                .Resolve(context => context.Source.IndexedProduct.ImgSrc);

            Field(d => d.IndexedProduct.OuterId, nullable: true).Description("The outer identifier");
            Field(d => d.IndexedProduct.Gtin, nullable: true).Description("Global Trade Item Number (GTIN)");
            Field(d => d.IndexedProduct.ManufacturerPartNumber, nullable: true).Description("Manufacturer Part Number (MPN)");
            Field(d => d.IndexedProduct.WeightUnit, nullable: true).Description("Weight unit");
            Field(d => d.IndexedProduct.Weight, nullable: true).Description("Weight");
            Field(d => d.IndexedProduct.MeasureUnit, nullable: true).Description("Measure unit");
            Field(d => d.IndexedProduct.Height, nullable: true).Description("Height");
            Field(d => d.IndexedProduct.Width, nullable: true).Description("Width");
            Field(d => d.IndexedProduct.Length, nullable: true).Description("Length");

            Field<StringGraphType>("brandName")
                .Description("Get brandName for product.")
                .Resolve(context =>
                {
                    var brandName = context.Source.IndexedProduct.Properties
                        ?.FirstOrDefault(x => x.Name.EqualsIgnoreCase("Brand"))
                        ?.Values
                        ?.FirstOrDefault(x => x.Value != null)
                        ?.Value;

                    return brandName?.ToString();
                });

            ExtendableFieldAsync<VariationType>(
                "masterVariation",
                resolve: async context =>
                {
                    if (string.IsNullOrEmpty(context.Source.IndexedProduct.MainProductId))
                    {
                        return null;
                    }

                    var query = context.GetCatalogQuery<LoadProductsQuery>();
                    query.ObjectIds = new[] { context.Source.IndexedProduct.MainProductId };
                    query.IncludeFields = context.SubFields.Values.GetAllNodesPaths(context).ToArray();

                    var response = await mediator.Send(query);

                    return response.Products.Select(expProduct => new ExpVariation(expProduct)).FirstOrDefault();
                });

            ExtendableFieldAsync<NonNullGraphType<ListGraphType<NonNullGraphType<VariationType>>>>(
                "variations",
                resolve: async context => await ResolveVariationsFieldAsync(mediator, context));

            Field<NonNullGraphType<BooleanGraphType>, bool>("hasVariations")
                .Resolve(context =>
                {
                    var result = context.Source.IndexedVariationIds?.Any() ?? false;
                    return result;
                });

            ExtendableField<NonNullGraphType<AvailabilityDataType>>(
                "availabilityData",
                "Product availability data",
                resolve: context => AbstractTypeFactory<ExpAvailabilityData>.TryCreateInstance().FromProduct(context.Source));

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<ImageType>>>>(
                "images",
                "Product images",
                resolve: context =>
                {
                    var images = context.Source.IndexedProduct.Images ?? Array.Empty<Image>();

                    return context.GetValue<string>("cultureName") switch
                    {
                        // Get images with null or current cultureName value if cultureName is passed
                        string languageCode => images.Where(x => string.IsNullOrEmpty(x.LanguageCode) || x.LanguageCode.EqualsIgnoreCase(languageCode)).ToList(),

                        // CultureName is null
                        _ => images
                    };
                });

            ExtendableField<NonNullGraphType<PriceType>>(
                "price",
                "Product price",
                resolve: context => context.Source.AllPrices.FirstOrDefault() ?? new ProductPrice(context.GetCurrencyByCode(context.GetValue<string>("currencyCode"))));

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<PriceType>>>>(
                "prices",
                "Product prices",
                resolve: context => context.Source.AllPrices);

            ExtendableField<PriceType>(
                "minVariationPrice",
                "Minimum product variation price",
                resolve: context => context.Source.MinVariationPrice);

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<PropertyType>>>>("properties",
                arguments: new QueryArguments(new QueryArgument<ListGraphType<StringGraphType>> { Name = "names" }),
                resolve: context =>
            {
                var names = context.GetArgument<string[]>("names");
                var cultureName = context.GetValue<string>("cultureName");
                var result = context.Source.IndexedProduct.Properties.ExpandOrderedByValues(cultureName);
                if (!names.IsNullOrEmpty())
                {
                    result = result.Where(x => names.Contains(x.Name, StringComparer.InvariantCultureIgnoreCase)).ToList();
                }
                return result;
            });

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<PropertyType>>>>("keyProperties",
                arguments: new QueryArguments(new QueryArgument<IntGraphType> { Name = "take" }),
                resolve: context =>
                {
                    var take = context.GetArgument<int>("take");
                    var cultureName = context.GetValue<string>("cultureName");

                    var result = context.Source.IndexedProduct.Properties.ExpandKeyPropertiesByValues(cultureName, take);

                    return result;
                });

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<AssetType>>>>(
                "assets",
                "Assets",
                resolve: context =>
                {
                    var assets = context.Source.IndexedProduct.Assets ?? Array.Empty<Asset>();

                    return context.GetValue<string>("cultureName") switch
                    {
                        // Get assets with null or current cultureName value if cultureName is passed
                        string languageCode => assets.Where(x => string.IsNullOrEmpty(x.LanguageCode) || x.LanguageCode.EqualsIgnoreCase(languageCode)).ToList(),

                        // CultureName is null
                        _ => assets
                    };
                });

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<OutlineType>>>>("outlines", "Outlines", resolve: context => context.Source.IndexedProduct.Outlines ?? Array.Empty<Outline>());

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<BreadcrumbType>>>>(
                "breadcrumbs",
                "Breadcrumbs",
                resolve: context => context.Source.IndexedProduct.Outlines.GetBreadcrumbs(context));

            ExtendableField<VendorType>("vendor",
                "Product vendor",
                resolve: context => context.Source.Vendor);

            ExtendableField<RatingType>(
                "rating",
                "Product rating",
                resolve: context => context.Source.Rating);


            Field(x => x.InWishlist, nullable: false).Description("Product added at least in one wishlist");

            Field(x => x.WishlistIds, nullable: false).Description("List of wishlist ID with this product");

            Field(x => x.IsPurchased, nullable: false).Description("Product was purchased");

            Connection<ProductAssociationType>("associations")
              .Argument<StringGraphType>("query", "the search phrase")
              .Argument<StringGraphType>("group", "association group (Accessories, RelatedItem)")
              .PageSize(Connections.DefaultPageSize)
              .ResolveAsync(async context => await ResolveAssociationConnectionAsync(mediator, context));


            Connection<VideoType>("videos")
              .PageSize(Connections.DefaultPageSize)
              .ResolveAsync(async context => await ResolveVideosConnectionAsync(mediator, context));
        }

        protected virtual async Task<object> ResolveVariationsFieldAsync(IMediator mediator, IResolveFieldContext<ExpProduct> context)
        {
            if (context.Source.IndexedVariationIds.IsNullOrEmpty())
            {
                return new List<ExpVariation>();
            }

            var query = context.GetCatalogQuery<LoadProductsQuery>();
            query.ObjectIds = context.Source.IndexedVariationIds;
            query.IncludeFields = context.SubFields.Values.GetAllNodesPaths(context).ToList();

            // Include "isActive" field to filter out inactive variations
            if (!query.IncludeFields.Contains("isActive"))
            {
                query.IncludeFields.Add("isActive");
            }

            var response = await mediator.Send(query);

            return response.Products.Where(x => x.IndexedProduct?.IsActive == true).Select(expProduct => new ExpVariation(expProduct));
        }

        private static async Task<object> ResolveAssociationConnectionAsync(IMediator mediator, IResolveConnectionContext<ExpProduct> context)
        {
            var first = context.First;

            int.TryParse(context.After, out var skip);

            var query = new SearchProductAssociationsQuery
            {
                Skip = skip,
                Take = first ?? context.PageSize ?? 10,

                Keyword = context.GetArgument<string>("query"),
                Group = context.GetArgument<string>("group"),
                ObjectIds = [context.Source.IndexedProduct.Id]
            };

            var response = await mediator.Send(query);

            return new PagedConnection<ProductAssociation>(response.Result.Results, query.Skip, query.Take, response.Result.TotalCount);
        }

        private static async Task<object> ResolveVideosConnectionAsync(IMediator mediator, IResolveConnectionContext<ExpProduct> context)
        {
            var first = context.First;

            int.TryParse(context.After, out var skip);

            var query = new SearchVideoQuery
            {
                Skip = skip,
                Take = first ?? context.PageSize ?? 10,
                OwnerType = "Product",
                OwnerId = context.Source.Id,
                CultureName = context.GetArgumentOrValue<string>("cultureName")
            };

            var response = await mediator.Send(query);

            return new PagedConnection<Video>(response.Result.Results, query.Skip, query.Take, response.Result.TotalCount);
        }
    }
}
