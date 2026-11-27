using System.Collections.Generic;
using FluentAssertions;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Models
{
    // VCST-6824: the catalog price must equal the sale price and the cart price under any currency rounding policy
    public class ExpProductPriceRoundingTests
    {
        public static TheoryData<string, int, decimal, decimal, decimal> StaticDiscountCases()
        {
            var data = new TheoryData<string, int, decimal, decimal, decimal>();
            var roundingTypes = new[] { "Rounding001", "Rounding005Up", "Rounding005Down", "Rounding01Up", "Rounding01Down", "Rounding05", "Rounding1", "Rounding1Up" };
            var prices = new (decimal List, decimal Sale, decimal Tax)[]
            {
                (12.5549m, 6.52m, 0.20m),   // list price with 4 decimals (VCST-6824)
                (12.5649m, 6.53m, 0.21m),   // VP-9367
                (19.99m, 14.97m, 0.081m),   // ordinary prices, cash rounding breaks them today
                (10.50m, 7.20m, 0.20m),
                (20.00m, 15.00m, 0.20m),    // nothing to round
            };

            foreach (var roundingType in roundingTypes)
            {
                foreach (var (list, sale, tax) in prices)
                {
                    data.Add(roundingType, 2, list, sale, tax);
                }
            }

            // currency without decimals
            data.Add("Rounding001", 0, 10.50m, 7.20m, 0.20m);
            data.Add("Rounding001", 0, 10.50m, 7.20m, 0m);

            return data;
        }

        [Theory]
        [MemberData(nameof(StaticDiscountCases))]
        public void ApplyStaticDiscounts_AnyRoundingPolicy_ActualEqualsSale(string roundingType, int decimalDigits, decimal list, decimal sale, decimal tax)
        {
            // Arrange
            var currency = CreateCurrency(roundingType, decimalDigits);
            var price = CreatePrice(currency, list, sale, tax);
            var product = new ExpProduct { AllPrices = new List<ProductPrice> { price } };

            // Act
            product.ApplyStaticDiscounts();

            // Assert
            price.ActualPrice.InternalAmount.Should().Be(sale);
            price.ActualPrice.Amount.Should().Be(price.SalePrice.Amount);
            price.ActualPriceWithTax.Amount.Should().Be(price.SalePriceWithTax.Amount);
        }

        [Theory]
        [MemberData(nameof(StaticDiscountCases))]
        public void ApplyRewards_NoValidRewards_ActualEqualsSale(string roundingType, int decimalDigits, decimal list, decimal sale, decimal tax)
        {
            // Arrange
            var currency = CreateCurrency(roundingType, decimalDigits);
            var price = CreatePrice(currency, list, sale, tax);
            var product = new ExpProduct { AllPrices = new List<ProductPrice> { price } };
            var reward = CreatePercentReward(15m);
            reward.IsValid = false;

            // Act
            product.ApplyRewards([reward]);

            // Assert
            price.ActualPrice.InternalAmount.Should().Be(sale);
            price.ActualPriceWithTax.Amount.Should().Be(price.SalePriceWithTax.Amount);
        }

        [Fact]
        public void ApplyRewards_PercentReward_BaseIsExactPriceAfterSale()
        {
            // Arrange: 0.05 cash rounding; today the promotion base is rounded 8.01 -> 8.05
            var currency = CreateCurrency("Rounding005Up", 2);
            var price = CreatePrice(currency, 24.99m, 8.01m, 0.20m);
            var product = new ExpProduct { AllPrices = new List<ProductPrice> { price } };

            // Act
            product.ApplyRewards([CreatePercentReward(15m)]);

            // Assert: same base as the cart line item, 8.01 - 15% = 6.8085
            price.ActualPrice.InternalAmount.Should().Be(6.8085m);
            price.ActualPrice.Amount.Should().Be(6.85m);
            price.Discounts.Should().ContainSingle().Which.DiscountAmount.Should().Be(1.2015m);
        }

        [Fact]
        public void ApplyRewards_RoundAmountPerItem_RewardRoundedOnce()
        {
            // Arrange
            var currency = CreateCurrency("Rounding001", 2);
            var price = CreatePrice(currency, 12.5549m, 6.52m, 0.20m);
            var product = new ExpProduct { AllPrices = new List<ProductPrice> { price } };
            var reward = CreateAbsoluteReward(0.333m);
            reward.RoundAmountPerItem = true;

            // Act
            product.ApplyRewards([reward]);

            // Assert
            price.ActualPrice.InternalAmount.Should().Be(6.19m);
            price.ActualPriceWithTax.Amount.Should().Be(7.43m);
        }

        [Theory]
        [InlineData("Rounding001", 0, 10.50, 7.20, 0.20, 11, 4, 7)]
        [InlineData("Rounding001", 2, 12.5549, 6.52, 0.20, 12.55, 6.03, 6.52)]
        [InlineData("Rounding005Up", 2, 19.99, 14.97, 0.081, 20.00, 5.00, 15.00)]
        public void DisplayDiscountAmount_ListMinusDiscountEqualsActual(string roundingType, int decimalDigits, decimal list, decimal sale, decimal tax,
            decimal expectedList, decimal expectedDiscount, decimal expectedActual)
        {
            // Arrange
            var currency = CreateCurrency(roundingType, decimalDigits);
            var price = CreatePrice(currency, list, sale, tax);
            var product = new ExpProduct { AllPrices = new List<ProductPrice> { price } };
            product.ApplyStaticDiscounts();

            // Act
            var discount = price.GetDisplayDiscountAmount();
            var discountWithTax = price.GetDisplayDiscountAmountWithTax();

            // Assert
            price.ListPrice.Amount.Should().Be(expectedList);
            discount.Amount.Should().Be(expectedDiscount);
            price.ActualPrice.Amount.Should().Be(expectedActual);
            (price.ListPrice.Amount - discount.Amount).Should().Be(price.ActualPrice.Amount);
            (price.ListPriceWithTax.Amount - discountWithTax.Amount).Should().Be(price.ActualPriceWithTax.Amount);
        }

        [Fact]
        public void DisplayDiscountAmount_SaleAboveList_Zero()
        {
            // Arrange
            var currency = CreateCurrency("Rounding001", 2);
            var price = CreatePrice(currency, 50m, 85m, 0.20m);
            var product = new ExpProduct { AllPrices = new List<ProductPrice> { price } };
            product.ApplyStaticDiscounts();

            // Act + Assert
            price.GetDisplayDiscountAmount().Amount.Should().Be(0m);
            price.ActualPrice.Amount.Should().Be(50m);
        }

        [Fact]
        public void RewardExtensions_ApplyRewards_DiscountsAreExact()
        {
            // Arrange
            var currency = CreateCurrency("Rounding001", 2);
            var price = CreatePrice(currency, 10.0049m, 10.0049m, 0.20m);
            price.TierPrices.Add(new TierPrice(new Money(8.0049m, currency), 5));
            var prices = new List<ProductPrice> { price };
            var reward = CreatePercentReward(10m);

            // Act
            prices.ApplyRewards([reward]);

            // Assert
            price.DiscountAmount.InternalAmount.Should().Be(1.00049m);
            price.TierPrices[0].DiscountAmount.InternalAmount.Should().Be(10.0049m - 8.0049m + 0.80049m);
        }

        private static Currency CreateCurrency(string roundingType, int decimalDigits)
        {
            return new Currency(Language.InvariantLanguage, "TST")
            {
                DecimalDigits = decimalDigits,
                RoundingType = roundingType,
                MidpointRounding = "AwayFromZero",
                RoundingPolicy = new DefaultMoneyRoundingPolicy(),
                ExchangeRate = 1m,
            };
        }

        private static ProductPrice CreatePrice(Currency currency, decimal list, decimal sale, decimal tax)
        {
            return new ProductPrice(currency)
            {
                ProductId = "product",
                ListPrice = new Money(list, currency),
                SalePrice = new Money(sale, currency),
                TaxPercentRate = tax,
            };
        }

        private static CatalogItemAmountReward CreatePercentReward(decimal percent)
        {
            return new CatalogItemAmountReward
            {
                IsValid = true,
                AmountType = RewardAmountType.Relative,
                Amount = percent,
                Promotion = new Promotion { Id = "promotion", Description = "test" },
            };
        }

        private static CatalogItemAmountReward CreateAbsoluteReward(decimal amount)
        {
            return new CatalogItemAmountReward
            {
                IsValid = true,
                AmountType = RewardAmountType.Absolute,
                Amount = amount,
                Promotion = new Promotion { Id = "promotion", Description = "test" },
            };
        }
    }
}
