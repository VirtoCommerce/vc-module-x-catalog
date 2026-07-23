using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Extensions;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Extensions;

public class PropertyExtensionsTests
{
    private const string CultureName = "en-US";

    [Fact]
    public void ExpandByValues_NonDictionary_WrapsEachMatchingValueIntoOwnProperty()
    {
        var value1 = new PropertyValue { Value = "Red", LanguageCode = CultureName };
        var value2 = new PropertyValue { Value = "Blue", LanguageCode = CultureName };
        var otherCulture = new PropertyValue { Value = "Rot", LanguageCode = "de-DE" };
        var property = new Property { Name = "Color", Values = new List<PropertyValue> { value1, value2, otherCulture } };

        var result = new[] { property }.ExpandByValues(CultureName);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(x => x.Name == "Color");
        result.Should().OnlyContain(x => !ReferenceEquals(x, property));
        result[0].Values.Should().ContainSingle().Which.Should().BeSameAs(value1);
        result[1].Values.Should().ContainSingle().Which.Should().BeSameAs(value2);
        property.Values.Should().HaveCount(3, "the source property must not be mutated");
    }

    [Fact]
    public void ExpandByValues_ExpandedCopiesHaveIndependentPropertyGraphs()
    {
        var property = new Property
        {
            Name = "Color",
            Values = new List<PropertyValue>
            {
                new() { Value = "Red", LanguageCode = CultureName },
                new() { Value = "Blue", LanguageCode = CultureName },
            },
            Attributes = new List<PropertyAttribute> { new() { Name = "attr", Value = "v" } },
        };

        var result = new[] { property }.ExpandByValues(CultureName);

        result[0].Attributes.Should().NotBeSameAs(property.Attributes);
        result[0].Attributes.Should().NotBeSameAs(result[1].Attributes);
        result[0].Attributes.Single().Should().NotBeSameAs(property.Attributes.Single());
    }

    [Fact]
    public void ExpandByValues_Dictionary_LocalizedValuePickedFallbackBuiltFromAlias()
    {
        var localized = new PropertyValue { Alias = "R", Value = "Red", LanguageCode = CultureName };
        var foreignOnly = new PropertyValue { Alias = "B", Value = "Blau", LanguageCode = "de-DE" };
        var property = new Property
        {
            Name = "Color",
            Dictionary = true,
            Values = new List<PropertyValue> { localized, foreignOnly },
        };

        var result = new[] { property }.ExpandByValues(CultureName);

        result.Should().HaveCount(2);
        result[0].Values.Should().ContainSingle().Which.Should().BeSameAs(localized);
        var fallback = result[1].Values.Should().ContainSingle().Subject;
        fallback.Should().NotBeSameAs(foreignOnly, "missing localization is replaced by a built default value");
        fallback.Value.Should().Be("B");
    }

    [Fact]
    public void ExpandByValues_NoMatchingValues_YieldsSinglePropertyWithEmptyValues()
    {
        var property = new Property
        {
            Name = "Color",
            Values = new List<PropertyValue> { new() { Value = "Rot", LanguageCode = "de-DE" } },
        };

        var result = new[] { property }.ExpandByValues(CultureName);

        result.Should().ContainSingle().Which.Values.Should().BeEmpty();
    }

    [Fact]
    public void ExpandByValues_HiddenPropertiesAreSkipped()
    {
        var property = new Property
        {
            Name = "Hidden",
            Hidden = true,
            Values = new List<PropertyValue> { new() { Value = "x", LanguageCode = CultureName } },
        };

        new[] { property }.ExpandByValues(CultureName).Should().BeEmpty();
    }

    [Fact]
    public void CopyPropertyWithValue_ReturnsCloneCarryingTheOriginalValueReference()
    {
        var property = new Property
        {
            Name = "Color",
            Values = new List<PropertyValue> { new() { Value = "Red", LanguageCode = CultureName } },
        };
        var value = new PropertyValue { Value = "Blue", LanguageCode = CultureName };

        var copy = value.CopyPropertyWithValue(property);

        copy.Should().NotBeSameAs(property);
        copy.Name.Should().Be("Color");
        copy.Values.Should().ContainSingle().Which.Should().BeSameAs(value);
        property.Values.Should().ContainSingle("the source property must not be mutated");
    }
}
