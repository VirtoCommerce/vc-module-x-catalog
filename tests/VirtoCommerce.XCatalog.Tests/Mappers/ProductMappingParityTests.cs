using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using FluentAssertions;
using Moq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Services;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Services;
using VirtoCommerce.XCatalog.Data.Services;
using VirtoCommerce.XCatalog.Tests.Helpers;
using Xunit;
using ProductPrice = VirtoCommerce.Xapi.Core.Models.ProductPrice;

namespace VirtoCommerce.XCatalog.Tests.Mappers;

/// <summary>
/// Compares <see cref="XCatalogMapper"/>'s <c>ToProductPromoEntry</c>/<c>ToTaxLines</c>/<c>ToProductPrices</c>
/// against <see cref="LegacyProductMappingProfile"/> - the real deleted AutoMapper logic - on the same inputs.
/// </summary>
public class ProductMappingParityTests
{
    private readonly IXCatalogMapper _mapper = new XCatalogMapper(Mock.Of<IFacetMapper>());

    private static readonly IMapper _legacyMapper = new MapperConfiguration(cfg =>
        cfg.AddProfile<LegacyProductMappingProfile>()).CreateMapper();

    static ProductMappingParityTests()
    {
        AbstractTypeFactory<ProductPrice>.RegisterType<ProductPrice>();
    }

    [Fact]
    public void ToProductPromoEntry_ProducesSameResultAsLegacyAutoMapperProfile()
    {
        var currency = CreateCurrency("USD");
        var otherCurrency = CreateCurrency("EUR");

        var product = new ExpProduct
        {
            IndexedProduct = new CatalogProduct
            {
                Id = "product-1",
                CatalogId = "catalog-1",
                CategoryId = "category-1",
                MainProductId = "parent-1",
                Code = "SKU-1",
                Category = new Category { Id = "category-1" },
            },
            AllPrices =
            [
                new ProductPrice(otherCurrency)
                {
                    ListPrice = new Money(20m, otherCurrency),
                    SalePrice = new Money(18m, otherCurrency),
                },
                new ProductPrice(currency)
                {
                    ListPrice = new Money(100m, currency),
                    SalePrice = new Money(80m, currency),
                    DiscountAmount = new Money(20m, currency),
                },
            ],
        };

        var expected = _legacyMapper.Map<ProductPromoEntry>(product, options => options.Items["currency"] = currency);

        var actual = _mapper.ToProductPromoEntry(product, SearchProductResponseBuilder.Build(currency).ToPromoPriceMappingContext());

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ToProductPromoEntry_NoPriceForRequestedCurrency_ProducesSameResultAsLegacyAutoMapperProfile()
    {
        var requestedCurrency = CreateCurrency("USD");
        var otherCurrency = CreateCurrency("EUR");

        var product = new ExpProduct
        {
            IndexedProduct = new CatalogProduct { Id = "product-1" },
            AllPrices = [new ProductPrice(otherCurrency) { ListPrice = new Money(20m, otherCurrency) }],
        };

        var expected = _legacyMapper.Map<ProductPromoEntry>(product, options => options.Items["currency"] = requestedCurrency);

        var actual = _mapper.ToProductPromoEntry(product, SearchProductResponseBuilder.Build(requestedCurrency).ToPromoPriceMappingContext());

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ToTaxLines_ProducesSameResultAsLegacyAutoMapperProfile()
    {
        var currency = CreateCurrency("USD");
        var priceWithTier = new ProductPrice(currency)
        {
            ListPrice = new Money(50m, currency),
            SalePrice = new Money(40m, currency),
            DiscountAmount = new Money(10m, currency),
        };
        priceWithTier.TierPrices.Add(new TierPrice(new Money(35m, currency), new Money(30m, currency), 5));

        var fullDiscountPrice = new ProductPrice(currency)
        {
            ListPrice = new Money(50m, currency),
            SalePrice = new Money(0m, currency),
            DiscountAmount = new Money(50m, currency),
        };

        var product = new ExpProduct
        {
            IndexedProduct = new CatalogProduct { Id = "product-1", Code = "SKU-1", Name = "Widget", TaxType = "TaxA" },
            AllPrices = [priceWithTier, fullDiscountPrice],
        };

        var expected = _legacyMapper.Map<IEnumerable<TaxLine>>(product).ToList();

        var actual = _mapper.ToTaxLines(product).ToList();

        actual.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
    }

    [Fact]
    public void ToProductPrices_ProducesSameResultAsLegacyAutoMapperProfile()
    {
        var usd = CreateCurrency("USD");
        var eur = CreateCurrency("EUR");
        var pricelist = new Pricelist { Id = "pl-1", Name = "Default" };

        var prices = new List<Price>
        {
            new() { Currency = "USD", ProductId = "p-1", PricelistId = "pl-1", List = 100m, Sale = 90m, MinQuantity = 5 },
            new() { Currency = "USD", ProductId = "p-1", PricelistId = "pl-1", List = 100m, Sale = 80m, MinQuantity = 1 },
            new() { Currency = "EUR", ProductId = "p-1", List = 40m, MinQuantity = 1 },
            new() { Currency = "GBP", ProductId = "p-1", List = 10m },
        };

        var expected = _legacyMapper.Map<IEnumerable<ProductPrice>>(prices, options =>
        {
            options.Items["all_currencies"] = new[] { usd, eur };
            options.Items["pricelists"] = new[] { pricelist };
        }).ToList();

        var actual = _mapper.ToProductPrices(prices, SearchProductResponseBuilder.Build(allStoreCurrencies: [usd, eur]).ToProductPricesMappingContext([pricelist])).ToList();

        actual.Should().BeEquivalentTo(expected);
    }

    private static Currency CreateCurrency(string code)
    {
        return new Currency(Language.InvariantLanguage, code) { RoundingPolicy = new DefaultMoneyRoundingPolicy() };
    }
}
