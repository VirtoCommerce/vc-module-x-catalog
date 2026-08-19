using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Services;
using VirtoCommerce.XCatalog.Web;
using Xunit;
using ProductPrice = VirtoCommerce.Xapi.Core.Models.ProductPrice;

namespace VirtoCommerce.XCatalog.Tests.Services;

public class XCatalogMapperTests
{
    private readonly IXCatalogMapper _mapper = new XCatalogMapper();

    static XCatalogMapperTests()
    {
        AbstractTypeFactory<ProductPrice>.RegisterType<ProductPrice>();
    }

    [Fact]
    public void Initialize_Registers_XCatalogMapper_AsSingleton()
    {
        var services = new ServiceCollection();

        new Module().Initialize(services);

        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(IXCatalogMapper));

        descriptor.Should().NotBeNull();
        descriptor.ImplementationType.Should().Be<XCatalogMapper>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void ToSearchProductQuery_MapsAllFields()
    {
        var source = new LoadProductsQuery
        {
            StoreId = "store-1",
            UserId = "user-1",
            CultureName = "en-US",
            CurrencyCode = "USD",
            PreviousOutline = "prev-outline",
            OrganizationId = "org-1",
            IncludeFields = ["price"],
            ObjectIds = ["p-1", "p-2"],
            EvaluatePromotions = false,
        };

        var result = _mapper.ToSearchProductQuery(source);

        result.StoreId.Should().Be("store-1");
        result.UserId.Should().Be("user-1");
        result.CultureName.Should().Be("en-US");
        result.CurrencyCode.Should().Be("USD");
        result.PreviousOutline.Should().Be("prev-outline");
        result.OrganizationId.Should().Be("org-1");
        result.IncludeFields.Should().BeEquivalentTo(["price"]);
        result.ObjectIds.Should().BeEquivalentTo(["p-1", "p-2"]);
        result.EvaluatePromotions.Should().BeFalse();

        result.Query.Should().BeNull();
        result.Filter.Should().BeNull();
        result.Sort.Should().BeNull();
    }

    [Fact]
    public void ToSearchProductQuery_NullSource_ReturnsNull()
    {
        _mapper.ToSearchProductQuery(null).Should().BeNull();
    }

    [Fact]
    public void ToSearchProductQuery_NullIncludeFields_MapsToEmptyCollection()
    {
        var source = new LoadProductsQuery { IncludeFields = null };

        var result = _mapper.ToSearchProductQuery(source);

        result.IncludeFields.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ToSearchCategoryQuery_MapsAllFields()
    {
        var source = new LoadCategoryQuery
        {
            StoreId = "store-1",
            UserId = "user-1",
            CultureName = "en-US",
            CurrencyCode = "USD",
            PreviousOutline = "prev-outline",
            OrganizationId = "org-1",
            IncludeFields = ["images"],
            ObjectIds = ["c-1"],
        };

        var result = _mapper.ToSearchCategoryQuery(source);

        result.StoreId.Should().Be("store-1");
        result.UserId.Should().Be("user-1");
        result.CultureName.Should().Be("en-US");
        result.CurrencyCode.Should().Be("USD");
        result.PreviousOutline.Should().Be("prev-outline");
        result.OrganizationId.Should().Be("org-1");
        result.IncludeFields.Should().BeEquivalentTo(["images"]);
        result.ObjectIds.Should().BeEquivalentTo(["c-1"]);

        result.Query.Should().BeNull();
        result.Filter.Should().BeNull();
        result.Sort.Should().BeNull();
    }

    [Fact]
    public void ToSearchCategoryQuery_NullIncludeFields_MapsToEmptyCollection()
    {
        var source = new LoadCategoryQuery { IncludeFields = null };

        var result = _mapper.ToSearchCategoryQuery(source);

        result.IncludeFields.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ToSearchCategoryQuery_NullSource_ReturnsNull()
    {
        _mapper.ToSearchCategoryQuery(null).Should().BeNull();
    }

    [Fact]
    public void ToProductAssociationSearchCriteria_MapsAllFields()
    {
        var source = new SearchProductAssociationsQuery
        {
            ObjectIds = ["p-1"],
            Keyword = "shoes",
            Group = "related",
            Sort = "name:asc",
            Skip = 5,
            Take = 10,
        };

        var result = _mapper.ToProductAssociationSearchCriteria(source);

        result.ObjectIds.Should().BeEquivalentTo(["p-1"]);
        result.Keyword.Should().Be("shoes");
        result.Group.Should().Be("related");
        result.Sort.Should().Be("name:asc");
        result.Skip.Should().Be(5);
        result.Take.Should().Be(10);

        result.Tags.Should().BeNull();
    }

    [Fact]
    public void ToProductAssociationSearchCriteria_NullSource_ReturnsNull()
    {
        _mapper.ToProductAssociationSearchCriteria(null).Should().BeNull();
    }

    [Fact]
    public void ToPropertyDictionaryItemSearchCriteria_MapsAllFields()
    {
        var source = new SearchPropertyDictionaryItemQuery
        {
            Skip = 2,
            Take = 20,
            PropertyIds = ["prop-1", "prop-2"],
        };

        var result = _mapper.ToPropertyDictionaryItemSearchCriteria(source);

        result.Skip.Should().Be(2);
        result.Take.Should().Be(20);
        result.PropertyIds.Should().BeEquivalentTo(["prop-1", "prop-2"]);

        result.CatalogIds.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ToPropertyDictionaryItemSearchCriteria_NullSource_ReturnsNull()
    {
        _mapper.ToPropertyDictionaryItemSearchCriteria(null).Should().BeNull();
    }

    [Fact]
    public void ToExpVendor_MapsIdNameAndType()
    {
        var member = new Vendor
        {
            Id = "vendor-1",
            Name = "Acme",
            MemberType = "Vendor",
        };

        var result = _mapper.ToExpVendor(member);

        result.Id.Should().Be("vendor-1");
        result.Name.Should().Be("Acme");
        result.Type.Should().Be("Vendor");
    }

    [Fact]
    public void ToExpVendor_NullSource_ReturnsNull()
    {
        _mapper.ToExpVendor(null).Should().BeNull();
    }

    [Fact]
    public void ToExpCategory_NullSource_ReturnsNull()
    {
        _mapper.ToExpCategory(null).Should().BeNull();
    }

    [Fact]
    public void ToExpProduct_NullSource_ReturnsNull()
    {
        _mapper.ToExpProduct(null).Should().BeNull();
    }

    [Fact]
    public void ToProductPromoEntry_MapsExpectedFieldsForRequestedCurrency()
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

        var result = _mapper.ToProductPromoEntry(product, currency);

        result.CatalogId.Should().Be("catalog-1");
        result.CategoryId.Should().Be("category-1");
        result.ProductId.Should().Be("product-1");
        result.ParentId.Should().Be("parent-1");
        result.Code.Should().Be("SKU-1");
        result.Outline.Should().Be("category-1");
        result.Price.Should().Be(80m);
        result.ListPrice.Should().Be(100m);
        result.Discount.Should().Be(20m);
        result.InStockQuantity.Should().Be(0);
        result.Quantity.Should().Be(1);
    }

    [Fact]
    public void ToProductPromoEntry_NoPriceForRequestedCurrency_LeavesPriceFieldsUnset()
    {
        var requestedCurrency = CreateCurrency("USD");
        var otherCurrency = CreateCurrency("EUR");

        var product = new ExpProduct
        {
            IndexedProduct = new CatalogProduct { Id = "product-1" },
            AllPrices = [new ProductPrice(otherCurrency) { ListPrice = new Money(20m, otherCurrency) }],
        };

        var result = _mapper.ToProductPromoEntry(product, requestedCurrency);

        result.Price.Should().Be(0m);
        result.ListPrice.Should().Be(0m);
        result.Discount.Should().Be(0m);
    }

    [Fact]
    public void ToProductPromoEntry_NullSource_ReturnsNull()
    {
        _mapper.ToProductPromoEntry(null, CreateCurrency("USD")).Should().BeNull();
    }

    [Fact]
    public void ToProductPromoEntry_NullCurrency_Throws()
    {
        var product = new ExpProduct { IndexedProduct = new CatalogProduct { Id = "product-1" } };

        FluentActions.Invoking(() => _mapper.ToProductPromoEntry(product, null))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToTaxLines_OnePriceWithoutTierPrices_ReturnsSingleLine()
    {
        var currency = CreateCurrency("USD");
        var price = new ProductPrice(currency)
        {
            ListPrice = new Money(50m, currency),
            SalePrice = new Money(40m, currency),
            DiscountAmount = new Money(10m, currency),
        };

        var product = new ExpProduct
        {
            IndexedProduct = new CatalogProduct { Id = "product-1", Code = "SKU-1", Name = "Widget", TaxType = "TaxA" },
            AllPrices = [price],
        };

        var lines = _mapper.ToTaxLines(product).ToList();

        lines.Should().HaveCount(1);
        lines[0].Id.Should().Be("product-1");
        lines[0].Code.Should().Be("SKU-1");
        lines[0].Name.Should().Be("Widget");
        lines[0].TaxType.Should().Be("TaxA");
        lines[0].Amount.Should().Be(40m);
    }

    [Fact]
    public void ToTaxLines_PriceWithTierPrices_AddsOneLinePerTier()
    {
        var currency = CreateCurrency("USD");
        var price = new ProductPrice(currency)
        {
            ListPrice = new Money(50m, currency),
            SalePrice = new Money(40m, currency),
            DiscountAmount = new Money(10m, currency),
        };

        price.TierPrices.Add(new TierPrice(new Money(35m, currency), new Money(30m, currency), 5));

        var product = new ExpProduct
        {
            IndexedProduct = new CatalogProduct { Id = "product-1", Code = "SKU-1", Name = "Widget", TaxType = "TaxA" },
            AllPrices = [price],
        };

        var lines = _mapper.ToTaxLines(product).ToList();

        lines.Should().HaveCount(2);
        lines[1].Quantity.Should().Be(5);
        lines[1].Amount.Should().Be(35m);
    }

    [Fact]
    public void ToTaxLines_FullDiscount_FallsBackToSalePrice()
    {
        var currency = CreateCurrency("USD");
        var price = new ProductPrice(currency)
        {
            ListPrice = new Money(50m, currency),
            SalePrice = new Money(0m, currency),
            DiscountAmount = new Money(50m, currency),
        };

        var product = new ExpProduct
        {
            IndexedProduct = new CatalogProduct { Id = "product-1", Code = "SKU-1", Name = "Widget" },
            AllPrices = [price],
        };

        var lines = _mapper.ToTaxLines(product).ToList();

        lines.Should().HaveCount(1);
        lines[0].Amount.Should().Be(0m);
    }

    [Fact]
    public void ToTaxLines_NullSource_ReturnsNull()
    {
        _mapper.ToTaxLines(null).Should().BeNull();
    }

    [Fact]
    public void ToProductPrices_GroupsByCurrency_NominalPriceHasLowestMinQuantity()
    {
        var currency = CreateCurrency("USD");
        var prices = new List<Price>
        {
            new() { Currency = "USD", ProductId = "p-1", List = 100m, Sale = 90m, MinQuantity = 5 },
            new() { Currency = "USD", ProductId = "p-1", List = 100m, Sale = 80m, MinQuantity = 1 },
        };

        var result = _mapper.ToProductPrices(prices, [currency]).ToList();

        result.Should().HaveCount(1);
        result[0].MinQuantity.Should().Be(1);
        result[0].SalePrice.Amount.Should().Be(80m);
        result[0].TierPrices.Should().HaveCount(2);
    }

    [Fact]
    public void ToProductPrices_UnknownCurrency_IsSkipped()
    {
        var currency = CreateCurrency("USD");
        var prices = new List<Price> { new() { Currency = "GBP", ProductId = "p-1", List = 100m } };

        var result = _mapper.ToProductPrices(prices, [currency]).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void ToProductPrices_WithPricelist_SetsPricelistName()
    {
        var currency = CreateCurrency("USD");
        var pricelist = new Pricelist { Id = "pl-1", Name = "Default" };
        var prices = new List<Price> { new() { Currency = "USD", ProductId = "p-1", PricelistId = "pl-1", List = 100m } };

        var result = _mapper.ToProductPrices(prices, [currency], [pricelist]).ToList();

        result.Should().ContainSingle().Which.PricelistName.Should().Be("Default");
    }

    [Fact]
    public void ToProductPrices_NullSource_ReturnsNull()
    {
        _mapper.ToProductPrices(null, []).Should().BeNull();
    }

    [Fact]
    public void ToProductPrices_NullAllCurrencies_Throws()
    {
        var prices = new List<Price> { new() { Currency = "USD", ProductId = "p-1", List = 100m } };

        FluentActions.Invoking(() => _mapper.ToProductPrices(prices, null))
            .Should().Throw<ArgumentNullException>();
    }

    private static Currency CreateCurrency(string code)
    {
        return new Currency(Language.InvariantLanguage, code) { RoundingPolicy = new DefaultMoneyRoundingPolicy() };
    }
}
