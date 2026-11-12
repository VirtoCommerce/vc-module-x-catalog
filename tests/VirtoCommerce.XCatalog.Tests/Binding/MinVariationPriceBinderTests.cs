using System;
using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.CatalogModule.Core.Serialization;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Binding;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Binding;

public class MinVariationPriceBinderTests
{
    private const string PriceFieldName = "__minvariationprice";

    private readonly MinVariationPriceBinder _binder = new();

    [Fact]
    public void BindModel_OnlyElementsWithoutObjectRoot_ReturnsEmpty()
    {
        var result = (IList<Price>)_binder.BindModel(Document(new object[] { "also not json", "null" }));

        result.Should().BeEmpty();
    }

    /// <summary>
    /// An element whose root is not a JSON object carries no price, so it costs that price only — it must
    /// not fail the whole result page that <c>XCatalogMapper.ToExpProduct</c>
    /// (<c>GenericModelBinder&lt;ExpProduct&gt;</c>) maps.
    /// </summary>
    [Theory]
    [InlineData("also not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("123")]
    public void BindModel_ElementWithoutObjectRoot_IsSkippedAndTheRestAreBound(string unusable)
    {
        var payloads = Serialize(("USD", 10.5m));

        var result = (IList<Price>)_binder.BindModel(Document(new object[] { unusable, payloads[0] }));

        result.Should().ContainSingle().Which.Currency.Should().Be("USD");
    }

    /// <summary>
    /// Once the object has started, a broken element is a defect to surface, not a price to drop silently.
    /// </summary>
    [Fact]
    public void BindModel_MalformedPriceElement_Throws()
    {
        var payloads = Serialize(("USD", 10.5m));

        var act = () => _binder.BindModel(Document(new object[] { "{ not json", payloads[0] }));

        act.Should().Throw<JsonReaderException>();
    }

    /// <summary>
    /// A bind-level failure (a value that does not fit the contract) must not be swallowed either.
    /// </summary>
    [Fact]
    public void BindModel_PriceThatDoesNotFitTheContract_Throws()
    {
        var act = () => _binder.BindModel(Document(new object[] { "{\"currency\":\"USD\",\"value\":\"abc\"}" }));

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// Long-standing behaviour: structural records are a fallback for an array that yielded no price
    /// from a string, so in a mixed array they are dropped rather than merged.
    /// </summary>
    [Fact]
    public void BindModel_MixedStringAndStructuralArray_BindsTheStringsOnly()
    {
        var payloads = Serialize(("USD", 10.5m), ("EUR", 9.25m));

        var result = (IList<Price>)_binder.BindModel(Document(new object[] { payloads[0], JObject.Parse(payloads[1]) }));

        result.Should().ContainSingle().Which.Currency.Should().Be("USD");
    }

    [Fact]
    public void BindModel_StringPayloads_ProduceSamePricesAsStructuralPayloads()
    {
        var payloads = Serialize(("USD", 10.5m), ("EUR", 9.25m));

        var fromStrings = _binder.BindModel(Document(payloads));
        var fromJObjects = _binder.BindModel(Document(Array.ConvertAll(payloads, JObject.Parse)));

        fromStrings.Should().BeEquivalentTo(fromJObjects);
    }

    /// <summary>
    /// A single structural record is not wrapped in an array by every provider.
    /// </summary>
    [Fact]
    public void BindModel_SingleStructuralRecord_IsBound()
    {
        var result = (IList<Price>)_binder.BindModel(Document(JObject.Parse(Serialize(("USD", 10.5m))[0])));

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Currency = "USD", List = 10.5m });
    }

    /// <summary>
    /// Structural records are the fallback for an array whose strings all failed to parse; a provider
    /// storing them must not lose prices because the string branch stopped building a DOM.
    /// </summary>
    [Fact]
    public void BindModel_StructuralRecordsInArray_AreBound()
    {
        var payloads = Serialize(("USD", 10.5m), ("EUR", 9.25m));

        var result = (IList<Price>)_binder.BindModel(Document(Array.ConvertAll(payloads, JObject.Parse)));

        result.Should().HaveCount(2);
    }

    /// <summary>
    /// Fails on the pre-fix binder, which builds a Newtonsoft DOM per price and then converts it —
    /// so binding costs strictly more than parsing the same payloads alone.
    /// </summary>
    [Fact]
    public void BindModel_StringPayloads_CostLessThanParsingThemIntoDocuments()
    {
        const int iterations = 50;

        var payloads = Serialize(("USD", 10.5m), ("EUR", 9.25m), ("GBP", 8.75m));
        var document = Document(payloads);

        _binder.BindModel(document);
        Array.ConvertAll(payloads, JObject.Parse);

        var parseBytes = Measure(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                Array.ConvertAll(payloads, JObject.Parse);
            }
        });

        var bindBytes = Measure(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                _binder.BindModel(document);
            }
        });

        bindBytes.Should().BeLessThan(parseBytes,
            "binding serialized prices must not build a throwaway JSON DOM per price");
    }

    private static long Measure(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static SearchDocument Document(object priceFieldValue)
    {
        return new SearchDocument { { PriceFieldName, priceFieldValue } };
    }

    private static string[] Serialize(params (string Currency, decimal Value)[] prices)
    {
        return Array.ConvertAll(prices, x => ProductJsonSerializer.Serialize(new IndexedPrice
        {
            Currency = x.Currency,
            Value = x.Value,
        }));
    }
}
