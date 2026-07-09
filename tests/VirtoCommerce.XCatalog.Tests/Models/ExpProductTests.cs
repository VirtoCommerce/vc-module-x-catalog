using System.Collections.Generic;
using FluentAssertions;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Models;

public class ExpProductTests
{
    private const string CultureName = "en-US";

    [Fact]
    public void GetExpandedProperties_SameCulture_ReturnsSameInstance()
    {
        var product = CreateProduct();

        var first = product.GetExpandedProperties(CultureName);
        var second = product.GetExpandedProperties(CultureName);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetExpandedProperties_MatchesDirectExpansion()
    {
        var product = CreateProduct();

        var expanded = product.GetExpandedProperties(CultureName);
        var direct = product.IndexedProduct.Properties.ExpandOrderedByValues(CultureName);

        expanded.Should().BeEquivalentTo(direct, options => options.WithStrictOrdering());
    }

    [Fact]
    public void GetExpandedProperties_DifferentCulture_Recomputes()
    {
        var product = CreateProduct();

        var enUs = product.GetExpandedProperties(CultureName);
        var deDe = product.GetExpandedProperties("de-DE");

        deDe.Should().NotBeSameAs(enUs);
        deDe.Should().BeEquivalentTo(
            product.IndexedProduct.Properties.ExpandOrderedByValues("de-DE"),
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void GetExpandedProperties_NoIndexedProduct_ReturnsEmpty()
    {
        var product = new ExpProduct();

        product.GetExpandedProperties(CultureName).Should().BeEmpty();
    }

    private static ExpProduct CreateProduct()
    {
        return new ExpProduct
        {
            IndexedProduct = new CatalogProduct
            {
                Properties = new List<Property>
                {
                    new()
                    {
                        Name = "Color",
                        Values = new List<PropertyValue>
                        {
                            new() { Value = "Red", LanguageCode = CultureName },
                            new() { Value = "Blue", LanguageCode = CultureName },
                        },
                    },
                    new()
                    {
                        Name = "Size",
                        Values = new List<PropertyValue>
                        {
                            new() { Value = "L", LanguageCode = CultureName },
                        },
                    },
                },
            },
        };
    }
}
