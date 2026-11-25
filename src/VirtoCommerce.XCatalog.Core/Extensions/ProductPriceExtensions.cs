using System;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Xapi.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Extensions
{
    public static class ProductPriceExtensions
    {
        private const int DiscountPercentDecimalDigits = 4;

        /// <summary>
        /// The discount as the customer sees it: the displayed list price minus the displayed actual price.
        /// Each of the three amounts is rounded by the currency rounding policy on its own, so the exact discount
        /// rounded separately could break "list - discount = actual" on the storefront.
        /// </summary>
        public static Money GetDisplayDiscountAmount(this ProductPrice price)
        {
            ArgumentNullException.ThrowIfNull(price);

            return GetDisplayDifference(price.ListPrice, price.ActualPrice);
        }

        /// <summary>
        /// The discount with tax as the customer sees it: the displayed list price with tax minus the displayed actual price with tax.
        /// </summary>
        public static Money GetDisplayDiscountAmountWithTax(this ProductPrice price)
        {
            ArgumentNullException.ThrowIfNull(price);

            return GetDisplayDifference(price.ListPriceWithTax, price.ActualPriceWithTax);
        }

        /// <summary>
        /// The relative discount as the customer sees it: the displayed discount divided by the displayed list price,
        /// with the same precision as <see cref="ProductPrice.DiscountPercent"/>.
        /// </summary>
        public static decimal GetDisplayDiscountPercent(this ProductPrice price)
        {
            ArgumentNullException.ThrowIfNull(price);

            var listAmount = price.ListPrice.Amount;

            return listAmount > 0
                ? Math.Round(price.GetDisplayDiscountAmount().Amount / listAmount, DiscountPercentDecimalDigits, MidpointRounding.AwayFromZero)
                : 0;
        }

        private static Money GetDisplayDifference(Money list, Money actual)
        {
            return new Money(Math.Max(0, list.Amount - actual.Amount), list.Currency);
        }
    }
}
