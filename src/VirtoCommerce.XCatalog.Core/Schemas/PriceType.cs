using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class PriceType : ExtendableGraphType<ProductPrice>
    {
        public PriceType()
        {
            Field<NonNullGraphType<MoneyType>>("list")
                .Description("Price list")
                .Resolve(context => context.Source.ListPrice);
            Field<NonNullGraphType<MoneyType>>("listWithTax")
                .Description("Price list with tax")
                .Resolve(context => context.Source.ListPriceWithTax);
            Field<NonNullGraphType<MoneyType>>("sale")
                .Description("Sale price")
                .Resolve(context => context.Source.SalePrice);
            Field<NonNullGraphType<MoneyType>>("saleWithTax")
                .Description("Sale price with tax")
                .Resolve(context => context.Source.SalePriceWithTax);
            Field<NonNullGraphType<MoneyType>>("actual")
                .Description("Actual price")
                .Resolve(context => context.Source.ActualPrice);
            Field<NonNullGraphType<MoneyType>>("actualWithTax")
                .Description("Actual price with tax")
                .Resolve(context => context.Source.ActualPriceWithTax);
            Field<NonNullGraphType<MoneyType>>("discountAmount")
                .Description("Discount amount")
                .Resolve(context => context.Source.DiscountAmount);
            Field<NonNullGraphType<MoneyType>>("discountAmountWithTax")
                .Description("Discount amount with tax")
                .Resolve(context => context.Source.DiscountAmountWithTax);
            Field(d => d.DiscountPercent, nullable: false);
            Field<NonNullGraphType<StringGraphType>>("currency")
                .Description("Currency")
                .Resolve(context => context.Source.Currency.Code);
            Field<DateTimeGraphType>("startDate")
                .Description("Start date")
                .Resolve(context => context.Source.StartDate);
            Field<DateTimeGraphType>("endDate")
                .Description("End date")
                .Resolve(context => context.Source.EndDate);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<TierPriceType>>>>("tierPrices")
                .Description("Tier prices")
                .Resolve(context => context.Source.TierPrices);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CatalogDiscountType>>>>("discounts")
                .Description("Discounts")
                .Resolve(context => context.Source.Discounts);

            Field(d => d.PricelistId, nullable: true).Description("The product price list");
            Field(d => d.PricelistName, nullable: true).Description("The product price list name");
            Field(d => d.MinQuantity, nullable: true).Description("The product min qty");
        }
    }
}
