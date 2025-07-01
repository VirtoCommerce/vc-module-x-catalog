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
            Field<NonNullGraphType<StringGraphType>>("id")
                .Description("Id of variation.")
                .Resolve(context => context.Source.IndexedProduct.Id);

            Field<NonNullGraphType<StringGraphType>>("name")
                .Description("Name of variation.")
                .Resolve(context => context.Source.IndexedProduct.Name);

            Field<NonNullGraphType<StringGraphType>>("code")
                .Description("SKU of variation.")
                .Resolve(context => context.Source.IndexedProduct.Code);

            Field<StringGraphType>("productType")
                .Description("The type of product")
                .Resolve(context => context.Source.IndexedProduct.ProductType);

            Field<IntGraphType>("minQuantity")
                .Description("Min. quantity.")
                .Resolve(context => context.Source.IndexedProduct.MinQuantity);

            Field<IntGraphType>("maxQuantity")
                .Description("Max. quantity.")
                .Resolve(context => context.Source.IndexedProduct.MaxQuantity);

            Field<IntGraphType>("packSize")
               .Description("Defines the number of items in a package. Quantity step for your product's.")
               .Resolve(context => context.Source.IndexedProduct.PackSize);

            ExtendableField<NonNullGraphType<AvailabilityDataType>>(
                "availabilityData",
                "Availability data",
                resolve: context => AbstractTypeFactory<ExpAvailabilityData>.TryCreateInstance().FromProduct(context.Source));

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<ImageType>>>>(
                "images",
                "Product images",
                resolve: context => context.Source.IndexedProduct.Images ?? Array.Empty<Image>());

            ExtendableField<NonNullGraphType<PriceType>>(
                "price",
                "Product price",
                resolve: context => context.Source.AllPrices.FirstOrDefault() ?? new ProductPrice(context.GetCurrencyByCode(context.GetValue<string>("currencyCode"))));

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<PriceType>>>>(
                "prices",
                "Product prices",
                resolve: context => context.Source.AllPrices);

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<PropertyType>>>>("properties", resolve: context =>
            {
                var cultureName = context.GetValue<string>("cultureName");
                return context.Source.IndexedProduct.Properties.ExpandByValues(cultureName);
            });

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<AssetType>>>>(
                "assets",
                "Assets",
                resolve: context => context.Source.IndexedProduct.Assets ?? Array.Empty<Asset>());

            ExtendableField<ListGraphType<NonNullGraphType<OutlineType>>>(
                "outlines",
                "Outlines",
                resolve: context => context.Source.IndexedProduct.Outlines);

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

            ExtendableField<VendorType>(
                "vendor",
                "Product vendor",
                resolve: context => context.Source.Vendor);

            ExtendableField<RatingType>(
                "rating",
                "Product rating",
                resolve: context => context.Source.Rating);
        }
    }
}
