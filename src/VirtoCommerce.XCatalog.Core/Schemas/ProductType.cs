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
using VirtoCommerce.StoreModule.Core.Extensions;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.Xapi.Core.Schemas.ScalarTypes;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using static VirtoCommerce.Xapi.Core.ModuleConstants;
using SeoExtensions = VirtoCommerce.Seo.Core.Extensions.SeoExtensions;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class ProductType : ExtendableGraphType<ExpProduct>
    {
        private const int MaxVariationsBatchSize = 200;
        private const string TypeNameMetaField = "__typename";

        // Fields the master's own indexed document can answer without loading the variation. Deliberately a
        // named set rather than a general projection: every addition here changes which selections switch to
        // reading the active flag from the master's document instead of the variation's own, so it must stay
        // auditable.
        private static readonly string[] _masterAnswerableVariationFields = ["id"];

        private readonly IDataLoaderContextAccessor _dataLoader;

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
        public ProductType(IDataLoaderContextAccessor dataLoader)
        {
            _dataLoader = dataLoader;

            Name = "Product";
            Description = "Products are the sellable goods in an e-commerce project.";

            Field(d => d.IndexedProduct.Id, nullable: false).Description("The unique ID of the product.");
            Field(d => d.IndexedProduct.Code, nullable: false).Description("The product SKU.");
            Field<StringGraphType>("catalogId")
                .Description("The unique ID of the catalog")
                .Resolve(context => context.Source.IndexedProduct.CatalogId);
            Field(d => d.IndexedProduct.ProductType, nullable: true).Description("The type of product");
            Field(d => d.IndexedProduct.MinQuantity, nullable: true)
                .Description("Min. quantity")
                .Resolve(context => context.Source.IndexedProduct.MinQuantity.GetValueOrDefault() <= 0 ? 1 : context.Source.IndexedProduct.MinQuantity);
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

                        return await context.GetMediator().Send(query);
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

                var response = await context.GetMediator().Send(loadRelatedCatalogOutlineQuery);
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

                var response = await context.GetMediator().Send(loadRelatedSlugPathQuery);
                return response.Slug;
            }).Description("Request related slug for product");

            Field<NonNullGraphType<StringGraphType>>("name").Resolve(context =>
            {
                var cultureName = context.GetArgumentOrValue<string>("cultureName");
                var product = context.Source.IndexedProduct;

                if (!cultureName.IsNullOrEmpty())
                {
                    var localizedName = product.LocalizedName?.GetValue(cultureName);
                    if (!string.IsNullOrEmpty(localizedName))
                    {
                        return localizedName;
                    }
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
                    seoInfo = source.IndexedProduct.SeoInfos.GetBestMatchingSeoInfo(store, cultureName);
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

                    var response = await context.GetMediator().Send(loadCategoryQuery);

                    return response.Categories.FirstOrDefault();
                });

            Field<StoreUrlType>("imgSrc")
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
                    return GetBrandName(context);
                });

            var brandField = new FieldType
            {
                Name = "brand",
                Type = typeof(BrandType),
                Resolver = new FuncFieldResolver<ExpProduct, IDataLoaderResult<BrandAggregate>>(context =>
                {
                    var loader = dataLoader.Context.GetOrAddBatchLoader<string, BrandAggregate>("brandAggregateLoader", async (brandNames) =>
                    {
                        var brandsQuery = AbstractTypeFactory<SearchBrandQuery>.TryCreateInstance();

                        brandsQuery.StoreId = context.GetArgumentOrValue<Store>("store")?.Id;
                        brandsQuery.CultureName = context.GetArgumentOrValue<string>("cultureName");
                        brandsQuery.BrandNames = brandNames.ToList();
                        brandsQuery.Take = brandsQuery.BrandNames.Count;

                        var response = await context.GetMediator().Send(brandsQuery);

                        var result = response.Results.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
                        return result;
                    });

                    return loader.LoadAsync(GetBrandName(context));
                })
            };
            AddField(brandField);

            ExtendableFieldAsync<VariationType>(
                "masterVariation",
                resolve: context =>
                {
                    if (string.IsNullOrEmpty(context.Source.IndexedProduct.MainProductId))
                    {
                        return Task.FromResult<object>(null);
                    }

                    var includeFields = context.SubFields.Values.GetAllNodesPaths(context).ToList();

                    // Deliberately no IsActive filter on the result: a master product is not a variation and is
                    // not subject to the variations field's active-only contract.
                    var loader = GetVariationLoader(context, includeFields);

                    // Returned, never awaited - see the matching comment on ResolveVariationsFieldAsync.
                    return Task.FromResult<object>(loader
                        .LoadAsync(context.Source.IndexedProduct.MainProductId)
                        .Then(product => product is null ? null : new ExpVariation(product)));
                });

            ExtendableFieldAsync<NonNullGraphType<ListGraphType<NonNullGraphType<VariationType>>>>(
                "variations",
                resolve: ResolveVariationsFieldAsync);

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
                var result = context.Source.GetExpandedProperties(cultureName);
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
              .ResolveAsync(context => context.ResolveAssociationsConnectionAsync());


            Connection<VideoType>("videos")
              .PageSize(Connections.DefaultPageSize)
              .ResolveAsync(ResolveVideosConnectionAsync);
        }

        [Obsolete("Use the constructor without IMediator. The mediator is resolved from context.RequestServices per request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
        public ProductType(IMediator mediator, IDataLoaderContextAccessor dataLoader)
            : this(dataLoader)
        {
        }

        private static string GetBrandName(IResolveFieldContext<ExpProduct> context)
        {
            var brandName = context.Source.IndexedProduct.Properties
                ?.FirstOrDefault(x => x.Name.EqualsIgnoreCase("Brand"))
                ?.Values
                ?.FirstOrDefault(x => x.Value != null)
                ?.Value;

            return brandName?.ToString();
        }

        protected virtual Task<object> ResolveVariationsFieldAsync(IResolveFieldContext<ExpProduct> context)
        {
            if (context.Source.IndexedVariationIds.IsNullOrEmpty())
            {
                return Task.FromResult<object>(new List<ExpVariation>());
            }

            var includeFields = context.SubFields.Values.GetAllNodesPaths(context).ToList();

            // __typename is resolved from the schema and depends on no data, so a selection carrying it needs
            // exactly what the same selection without it needs. Excluding it widens nothing: the spec reserves
            // the __ prefix for introspection and graphql-dotnet rejects any other name that uses it when the
            // schema is built, and of the three meta-fields only __typename is reachable on a nested field -
            // __schema and __type exist on the root query type alone. Leaving it in would matter: clients that
            // normalise a cache add __typename to every selection set, so the gate below would never fire.
            var dataFields = includeFields.Where(x => x != TypeNameMetaField).ToList();

            // Count is read off the raw selection, not the filtered one: a caller selecting nothing at all has
            // asked for nothing and must not be served, while a caller selecting only __typename has asked for
            // something the master's document can answer.
            if (includeFields.Count > 0 &&
                dataFields.All(x => _masterAnswerableVariationFields.Contains(x)))
            {
                // The stored id list is already active-only: the indexer writes an id into the master's document
                // only inside its IsActive branch, and any variation change forces a full reindex of the master.
                // The filter this skips read the variation's own index document, not the database - LoadProductsQuery
                // is served by ISearchProvider.SearchAsync - so no live-to-stale transition happens here.
                return Task.FromResult<object>(context.Source.IndexedVariationIds
                    .Select(id =>
                    {
                        // Through the factory, not new: CatalogProduct is an overridable type, and downstream
                        // schemas cast IndexedProduct to their own derived type. A synthetic base instance is the
                        // one value in the system that would fail such a cast.
                        var indexedProduct = AbstractTypeFactory<CatalogProduct>.TryCreateInstance();
                        indexedProduct.Id = id;

                        return new ExpVariation(new ExpProduct { IndexedProduct = indexedProduct });
                    })
                    .ToList());
            }

            var loader = GetVariationLoader(context, includeFields);

            // Returned, never awaited: awaiting here dispatches one batch per node and restores the N sends.
            // Task.FromResult is required rather than decoration - Then returns IDataLoaderResult<T>, not a
            // Task, so returning it bare from a Task<object> method does not compile.
            return Task.FromResult<object>(loader
                .LoadAsync(context.Source.IndexedVariationIds)
                .Then(products => products
                    .Where(x => x?.IndexedProduct?.IsActive == true)
                    .Select(x => new ExpVariation(x))
                    .ToList()));
        }

        // Shared by the variations and masterVariation resolvers so both fold onto the same loader when a page
        // renders both fields. GetOrAddBatchLoader resolves a key through ConcurrentDictionary.GetOrAdd, so the
        // first registration for a key wins - one definition removes the question of whether two independently
        // written fetch funcs stay semantically equivalent as either one changes later. The key is built here
        // rather than passed in for the same reason: two call sites composing it separately would drift apart
        // into two loaders, which costs the second search back with nothing failing.
        private IDataLoader<string, ExpProduct> GetVariationLoader(IResolveFieldContext context, IList<string> includeFields)
        {
            // "isActive" is requested for both fields although only the variations field filters on it, so that
            // the two agree on a key.
            var loadedFields = includeFields.ToList();
            if (!loadedFields.Contains("isActive"))
            {
                loadedFields.Add("isActive");
            }

            // The field set is part of the key, not just of the query: IncludeFields is derived from
            // context.SubFields, so two aliases selecting different subfields would otherwise share one loader
            // and whichever registered second would be served an under-selected result.
            // The comparer here is the one that is not redundant. Equality on string defaults to ordinal, but
            // ORDERING defaults to the current culture - and sibling nodes of one request can be resolved on
            // threads whose culture differs, which would sort the same field set two ways, produce two keys and
            // quietly cost the second search.
            var loaderKey = $"product_variations_{string.Join(',', loadedFields.OrderBy(x => x, StringComparer.Ordinal))}";

            return _dataLoader.Context.GetOrAddBatchLoader<string, ExpProduct>(
                loaderKey,
                async ids =>
                {
                    var query = context.GetCatalogQuery<LoadProductsQuery>();
                    query.ObjectIds = ids.ToList();
                    query.IncludeFields = loadedFields;

                    var response = await context.GetMediator().Send(query);

                    return response.Products.ToDictionary(x => x.Id);
                },
                // Caps the keys handed to one fetch, so a page of masters carrying many variations each cannot
                // turn N moderate searches into one oversized peak of the same total size.
                maxBatchSize: MaxVariationsBatchSize);
        }

        private static async Task<object> ResolveVideosConnectionAsync(IResolveConnectionContext<ExpProduct> context)
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

            var response = await context.GetMediator().Send(query);

            return new PagedConnection<Video>(response.Result.Results, query.Skip, query.Take, response.Result.TotalCount);
        }
    }
}
