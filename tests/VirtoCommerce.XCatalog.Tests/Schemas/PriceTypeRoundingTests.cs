using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using GraphQL;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Schemas;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Schemas
{
    // VCST-6824: discountAmount and discountPercent are what the customer sees: list - discount = actual
    public class PriceTypeRoundingTests
    {
        private readonly PriceType _priceType = new();

        [Theory]
        // 0-digit currency: 11 - 4 = 7, 4 / 11
        [InlineData("Rounding001", 0, 10.50, 7.20, 4, 0.3636)]
        [InlineData("Rounding001", 2, 12.5549, 6.52, 6.03, 0.4805)]
        [InlineData("Rounding005Up", 2, 19.99, 14.97, 5.00, 0.25)]
        [InlineData("Rounding001", 2, 20.00, 15.00, 5.00, 0.25)]
        public async Task DiscountFields_MatchDisplayedListAndActual(string roundingType, int decimalDigits, decimal list, decimal sale,
            decimal expectedDiscountAmount, decimal expectedDiscountPercent)
        {
            // Arrange
            var price = CreatePrice(roundingType, decimalDigits, list, sale);
            var context = new ResolveFieldContext { Source = price };

            // Act
            var actual = (Money)await ResolveAsync("actual", context);
            var listPrice = (Money)await ResolveAsync("list", context);
            var discountAmount = (Money)await ResolveAsync("discountAmount", context);
            var discountPercent = (decimal)await ResolveAsync("discountPercent", context);

            // Assert
            discountAmount.Amount.Should().Be(expectedDiscountAmount);
            (listPrice.Amount - discountAmount.Amount).Should().Be(actual.Amount);
            discountPercent.Should().Be(expectedDiscountPercent);
        }

        [Fact]
        public async Task DiscountPercent_ZeroListPrice_Zero()
        {
            // Arrange
            var price = CreatePrice("Rounding001", 2, 0m, 0m);
            var context = new ResolveFieldContext { Source = price };

            // Act
            var discountPercent = (decimal)await ResolveAsync("discountPercent", context);

            // Assert
            discountPercent.Should().Be(0m);
        }

        private async Task<object> ResolveAsync(string fieldName, ResolveFieldContext context)
        {
            return await _priceType.Fields.First(x => x.Name.EqualsIgnoreCase(fieldName)).Resolver.ResolveAsync(context);
        }

        private static ProductPrice CreatePrice(string roundingType, int decimalDigits, decimal list, decimal sale)
        {
            var currency = new Currency(Language.InvariantLanguage, "TST")
            {
                DecimalDigits = decimalDigits,
                RoundingType = roundingType,
                MidpointRounding = "AwayFromZero",
                RoundingPolicy = new DefaultMoneyRoundingPolicy(),
                ExchangeRate = 1m,
            };
            var price = new ProductPrice(currency)
            {
                ProductId = "product",
                ListPrice = new Money(list, currency),
                SalePrice = new Money(sale, currency),
            };
            var product = new ExpProduct { AllPrices = new List<ProductPrice> { price } };
            product.ApplyStaticDiscounts();

            return price;
        }
    }
}
