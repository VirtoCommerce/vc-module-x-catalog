using System;
using System.Linq;
using GraphQL.Types;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class VariationType : ExtendableGraphType<ExpVariation>
    {
        public VariationType(IMediator mediator)
        {
            Field<NonNullGraphType<StringGraphType>>(
                "id",
                description: "Id of variation.",
                resolve: context => context.Source.IndexedProduct.Id
            );

            Field<NonNullGraphType<StringGraphType>>(
                "name",
                description: "Name of variation.",
                resolve: context => context.Source.IndexedProduct.Name
            );

            Field<NonNullGraphType<StringGraphType>>(
                "code",
                description: "SKU of variation.",
                resolve: context => context.Source.IndexedProduct.Code
            );

            Field<StringGraphType>(
                "productType",
                description: "The type of product",
                resolve: context => context.Source.IndexedProduct.ProductType);

            Field<IntGraphType>(
                "minQuantity",
                description: "Min. quantity.",
                resolve: context => context.Source.IndexedProduct.MinQuantity
            );

            Field<IntGraphType>(
                "maxQuantity",
                description: "Max. quantity.",
                resolve: context => context.Source.IndexedProduct.MaxQuantity
            );

            Field<IntGraphType>(
               "packSize",
               description: "Defines the number of items in a package. Quantity step for your product's.",
               resolve: context => context.Source.IndexedProduct.PackSize
           );

            ExtendableField<NonNullGraphType<AvailabilityDataType>>(
                "availabilityData",
                "Availability data",
                resolve: context => AbstractTypeFactory<ExpAvailabilityData>.TryCreateInstance().FromProduct(context.Source));

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<ImageType>>>>("images",
                "Product images",
                resolve: context => context.Source.IndexedProduct.Images ?? Array.Empty<Image>());

            Field<NonNullGraphType<PriceType>>("price",
                "Product price",
                resolve: context => context.Source.AllPrices.FirstOrDefault() ?? new ProductPrice(context.GetCurrencyByCode(context.GetValue<string>("currencyCode"))));

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<PriceType>>>>("prices",
                "Product prices",
                resolve: context => context.Source.AllPrices);

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<PropertyType>>>>("properties", resolve: context =>
            {
                var cultureName = context.GetValue<string>("cultureName");
                return context.Source.IndexedProduct.Properties.ExpandByValues(cultureName);
            });

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<AssetType>>>>("assets",
                "Assets",
                resolve: context => context.Source.IndexedProduct.Assets ?? Array.Empty<Asset>());

            Field<ListGraphType<NonNullGraphType<OutlineType>>>("outlines",
                "Outlines",
                resolve: context => context.Source.IndexedProduct.Outlines);

            FieldAsync<StringGraphType>("slug", resolve: async context =>
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
            }, description: "Request related slug for product");

            ExtendableField<VendorType>(
                "vendor",
                "Product vendor",
                resolve: context => context.Source.Vendor);

            ExtendableField<RatingType>(
                "rating",
                "Product raiting",
                resolve: context => context.Source.Rating);

        }
    }
}
