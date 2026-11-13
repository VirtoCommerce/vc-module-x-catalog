using System;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.CatalogModule.Core.Serialization;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Seo.Core.Models;
using VirtoCommerce.XCatalog.Core.Binding;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Binding;

public class CatalogProductBinderTests
{
    private const string ObjectFieldName = "__object";

    private readonly CatalogProductBinder _binder = new();

    [Fact]
    public void BindModel_NoObjectField_ReturnsNull()
    {
        var result = _binder.BindModel(new SearchDocument());

        result.Should().BeNull();
    }

    /// <summary>
    /// A payload whose root is not a JSON object carries no product, so it costs the caller this one
    /// product, never the whole result page: <c>BindModel</c> runs inside the <c>XCatalogMapper.ToExpProduct</c>
    /// conversion (<c>GenericModelBinder&lt;ExpProduct&gt;</c>) that maps every document of the page.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[{ \"id\": \"product-id\" }]")]
    [InlineData("\"a string\"")]
    [InlineData("123")]
    [InlineData("not json at all")]
    public void BindModel_PayloadWithoutObjectRoot_ReturnsNull(string json)
    {
        var result = _binder.BindModel(Document(json));

        result.Should().BeNull();
    }

    /// <summary>
    /// Once the object has started, a broken payload is a defect to surface, not a product to drop silently.
    /// </summary>
    [Fact]
    public void BindModel_MalformedObjectPayload_Throws()
    {
        var act = () => _binder.BindModel(Document("{ this is not json"));

        act.Should().Throw<JsonReaderException>();
    }

    /// <summary>
    /// A bind-level failure (a value that does not fit the contract) must not be swallowed either.
    /// </summary>
    [Fact]
    public void BindModel_PayloadThatDoesNotFitTheContract_Throws()
    {
        var act = () => _binder.BindModel(Document("{\"isActive\":\"not-a-bool\"}"));

        act.Should().Throw<JsonException>();
    }

    /// <summary>
    /// A comment before the root object does not make the payload unusable: it is skipped, as the token
    /// tree that the string branch used to build skipped it.
    /// </summary>
    [Fact]
    public void BindModel_CommentBeforeObject_BindsTheProduct()
    {
        var product = CreateProduct();
        var json = "/* stored */ " + ProductJsonSerializer.Serialize(product);

        var result = _binder.BindModel(Document(json)) as CatalogProduct;

        result.Should().NotBeNull();
        result.Id.Should().Be(product.Id);
    }

    /// <summary>
    /// The string branch and the structural branch feed the same product to every consumer;
    /// only the provider differs in which one it stores.
    /// </summary>
    [Fact]
    public void BindModel_StringPayload_ProducesSameProductAsStructuralPayload()
    {
        var json = ProductJsonSerializer.Serialize(CreateProduct());

        var fromString = _binder.BindModel(Document(json));
        var fromJObject = _binder.BindModel(Document(JObject.Parse(json)));

        fromString.Should().BeEquivalentTo(fromJObject);
    }

    /// <summary>
    /// A date-shaped or plain text in <see cref="PropertyValue.Value"/> (typed <see cref="object"/>)
    /// resolves to the same runtime type and value on the string branch and on the structural branch.
    /// </summary>
    [Theory]
    [InlineData("2026-08-04T10:20:30Z")]
    [InlineData("2026-08-04")]
    [InlineData("not-a-date")]
    public void BindModel_DateShapedPropertyValue_ResolvesIdenticallyOnBothBranches(string value)
    {
        var product = CreateProduct();
        product.Properties[0].Values[0].Value = value;
        var json = ProductJsonSerializer.Serialize(product);

        var fromString = _binder.BindModel(Document(json)) as CatalogProduct;
        var fromJObject = _binder.BindModel(Document(JObject.Parse(json))) as CatalogProduct;

        var boundFromString = fromString.Properties[0].Values[0].Value;
        var boundFromJObject = fromJObject.Properties[0].Values[0].Value;

        boundFromString.Should().Be(boundFromJObject);
        boundFromString.GetType().Should().Be(boundFromJObject.GetType());
    }

    /// <summary>
    /// Fails on the pre-fix binder, which builds a Newtonsoft DOM and then walks it to produce the
    /// product — so binding costs strictly more than parsing alone.
    /// </summary>
    [Fact]
    public void BindModel_StringPayload_CostsLessThanParsingItIntoADocument()
    {
        const int iterations = 50;

        var json = ProductJsonSerializer.Serialize(CreateProduct());
        var document = Document(json);

        // Warm up the serializer's contract cache and the reflection caches behind the property-binder loop.
        _binder.BindModel(document);
        JObject.Parse(json);

        var parseBytes = Measure(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                JObject.Parse(json);
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
            "binding a serialized payload must not build a throwaway JSON DOM on the way to the product");
    }

    private static long Measure(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static SearchDocument Document(object objectFieldValue)
    {
        return new SearchDocument { { ObjectFieldName, objectFieldValue } };
    }

    private static CatalogProduct CreateProduct()
    {
        return new CatalogProduct
        {
            Id = "product-id",
            Code = "SKU-1",
            Name = "Safety gloves",
            CatalogId = "catalog-id",
            CategoryId = "category-id",
            ProductType = "Physical",
            IsActive = true,
            IsBuyable = true,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Properties =
            [
                new Property
                {
                    Id = "property-id",
                    Name = "Material",
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.ShortText,
                    Values =
                    [
                        new PropertyValue
                        {
                            PropertyName = "Material",
                            ValueType = PropertyValueType.ShortText,
                            Value = "Nitrile",
                        },
                    ],
                },
            ],
            Images =
            [
                new Image { Id = "image-id", Url = "/assets/gloves.jpg", SortOrder = 1 },
            ],
            SeoInfos =
            [
                new SeoInfo { Id = "seo-id", SemanticUrl = "safety-gloves", LanguageCode = "en-US" },
            ],
            Outlines =
            [
                new Outline
                {
                    Items =
                    [
                        new OutlineItem { Id = "catalog-id", SeoObjectType = "Catalog" },
                        new OutlineItem { Id = "category-id", SeoObjectType = "Category" },
                    ],
                },
            ],
        };
    }
}
