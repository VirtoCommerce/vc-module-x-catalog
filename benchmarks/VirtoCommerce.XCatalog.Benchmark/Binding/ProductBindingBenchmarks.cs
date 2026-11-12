using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.CatalogModule.Core.Serialization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Seo.Core.Models;
using VirtoCommerce.Xapi.Core.Binding;
using VirtoCommerce.XCatalog.Core.Binding;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Benchmark.Binding;

// What binding ONE indexed product document costs: the work SearchProductQueryHandler.ConvertProducts does
// per hit, through XCatalogMapper.ToExpProduct -> GenericModelBinder<ExpProduct>.BindModel, which runs the
// [BindIndexField] binders of ExpProduct. The two expensive ones are measured on their own as well, so a
// change in the total can be attributed:
//
//   BindExpProduct         the production call (baseline)
//   BindCatalogProduct     CatalogProductBinder, field "__object"            - the whole product
//   BindMinVariationPrices MinVariationPriceBinder, field "__minvariationprice"
//
// The file uses only public API that exists on both sides of a change to the binders, so the same file can
// be dropped into a checkout of the base commit and into the changed one to compare like with like.
//
// The document is shaped like the one a search provider returns when it stores a product's object as a
// JSON string: "__object" is a JSON string written by ProductJsonSerializer, and "__minvariationprice" is
// an array of such strings.
//
// What this fixture does NOT reproduce, so the figures read as per-document binding cost, not request cost:
//   - A provider that hands "__object" back as a JObject takes a different branch of CatalogProductBinder.
//   - Real documents vary in size; the synthetic product is tuned to one measured stored-object length
//     (see ObjectCharacters). Set XCATALOG_BENCH_OBJECT_JSON to a file holding a real stored "__object" to
//     measure a document of your own; never commit such a file.
//   - The rest of the response materialisation (parsing the provider's response into SearchDocument),
//     and everything after binding (prices, inventory, DataLoader), is not included.
//   - A single document: a result page binds one per hit, so multiply by the page size.
[MemoryDiagnoser]
public class ProductBindingBenchmarks
{
    private const string ObjectJsonEnvironmentVariable = "XCATALOG_BENCH_OBJECT_JSON";

    // A representative length for a stored "__object" of a fully populated product.
    // The synthetic product is sized to land near it; the actual figure is printed at setup.
    private const int ObjectCharacters = 20_000;

    // Values per property, cycled: single-valued properties dominate a catalog, multivalue ones are the tail.
    private static readonly int[] _valueCounts = [1, 2, 1, 3];

    private static readonly DateTime _date = new(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

    private SearchDocument _document;
    private readonly CatalogProductBinder _catalogProductBinder = new();
    private readonly MinVariationPriceBinder _minVariationPriceBinder = new();

    [GlobalSetup]
    public void Setup()
    {
        string objectJson;
        var path = Environment.GetEnvironmentVariable(ObjectJsonEnvironmentVariable);

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            objectJson = File.ReadAllText(path);
            Console.WriteLine($"__object source: file {path}, {objectJson.Length} characters");
        }
        else
        {
            objectJson = ProductJsonSerializer.Serialize(CreateProduct());
            Console.WriteLine($"__object source: synthetic product, {objectJson.Length} characters (target {ObjectCharacters})");
        }

        var minVariationPrices = new object[]
        {
            ProductJsonSerializer.Serialize(new IndexedPrice { Currency = "USD", Value = 12.5m }),
            ProductJsonSerializer.Serialize(new IndexedPrice { Currency = "EUR", Value = 11.75m }),
            ProductJsonSerializer.Serialize(new IndexedPrice { Currency = "GBP", Value = 9.99m }),
        };

        // Scalar fields a stored product document carries next to the two JSON strings.
        _document = new SearchDocument
        {
            Id = "product-000001",
            ["id"] = "product-000001",
            ["code"] = "SKU-000001",
            ["name"] = "Nitrile coated safety gloves, size L",
            ["catalog"] = "catalog-id",
            ["status"] = "visible",
            ["is"] = new object[] { "product", "visible" },
            ["__outline"] = new object[] { "catalog-id", "catalog-id/category-id" },
            ["__variations"] = new List<object> { "variation-000001", "variation-000002" },
            ["__score"] = 1.0,
            ["__object"] = objectJson,
            ["__minvariationprice"] = minVariationPrices,
        };

        // The binder constructed directly defaults its BindingInfo to the field it reads; set explicitly so
        // the field name the benchmark relies on is stated rather than assumed.
        _catalogProductBinder.BindingInfo = new BindingInfo { FieldName = "__object" };
        _minVariationPriceBinder.BindingInfo = new BindingInfo { FieldName = "__minvariationprice" };
    }

    /// <summary>The production call, as made by <c>XCatalogMapper.ToExpProduct</c>.</summary>
    [Benchmark(Baseline = true)]
    public object BindExpProduct() => new GenericModelBinder<ExpProduct>().BindModel(_document);

    [Benchmark]
    public object BindCatalogProduct() => _catalogProductBinder.BindModel(_document);

    [Benchmark]
    public object BindMinVariationPrices() => _minVariationPriceBinder.BindModel(_document);

    private static CatalogProduct CreateProduct()
    {
        var product = AbstractTypeFactory<CatalogProduct>.TryCreateInstance();

        product.Id = "product-000001";
        product.Code = "SKU-000001";
        product.Name = "Nitrile coated safety gloves, size L";
        product.CatalogId = "catalog-id";
        product.CategoryId = "category-id";
        product.OuterId = "ERP-000001";
        product.ProductType = "Physical";
        product.Gtin = "0123456789012";
        product.ManufacturerPartNumber = "MPN-000001";
        product.IsActive = true;
        product.IsBuyable = true;
        product.TrackInventory = true;
        product.StartDate = _date;
        product.EndDate = _date.AddYears(10);
        product.Priority = 10;
        product.PackSize = 12;
        product.MinQuantity = 1;
        product.MaxQuantity = 1000;
        product.WeightUnit = "kg";
        product.Weight = 0.35m;
        product.MeasureUnit = "cm";
        product.Height = 4.5m;
        product.Length = 24m;
        product.Width = 11m;
        product.TaxType = "Goods";
        product.CreatedDate = _date;
        product.CreatedBy = "admin";
        product.ModifiedDate = _date;
        product.ModifiedBy = "admin";

        product.Properties = CreateProperties();
        product.Images = CreateImages();
        product.Assets = CreateAssets();
        product.Outlines = CreateOutlines();
        product.SeoInfos = CreateSeoInfos("product-000001", "nitrile-coated-safety-gloves-size-l");
        product.Reviews = CreateReviews();
        product.Links = CreateLinks();

        return product;
    }

    private static List<Property> CreateProperties()
    {
        var valueTypes = new[]
        {
            PropertyValueType.ShortText,
            PropertyValueType.Number,
            PropertyValueType.Boolean,
            PropertyValueType.DateTime,
        };

        return Enumerable.Range(0, 16).Select(i =>
        {
            var valueType = valueTypes[i % valueTypes.Length];
            var name = $"Property{i:D2}";

            return new Property
            {
                Id = $"property-{i:D6}",
                Name = name,
                CatalogId = "catalog-id",
                CategoryId = "category-id",
                Type = PropertyType.Product,
                ValueType = valueType,
                Multivalue = i % 5 == 0,
                Values = Enumerable.Range(0, _valueCounts[i % _valueCounts.Length]).Select(j => new PropertyValue
                {
                    Id = $"value-{i:D6}-{j}",
                    PropertyId = $"property-{i:D6}",
                    PropertyName = name,
                    Alias = $"{name}-alias-{j}",
                    LanguageCode = j % 2 == 0 ? "en-US" : "de-DE",
                    ValueType = valueType,
                    Value = CreateValue(valueType, i, j),
                }).ToList(),
            };
        }).ToList();
    }

    private static object CreateValue(PropertyValueType valueType, int i, int j)
    {
        return valueType switch
        {
            PropertyValueType.Number => (decimal)(i * 10 + j) + 0.5m,
            PropertyValueType.Boolean => (i + j) % 2 == 0,
            PropertyValueType.DateTime => _date.AddDays(i * 3 + j),
            _ => $"Value of property {i:D2}, variant {j}, as a short text",
        };
    }

    private static List<Image> CreateImages()
    {
        return Enumerable.Range(0, 8).Select(i => new Image
        {
            Id = $"image-{i:D6}",
            Name = $"gloves-{i}.jpg",
            Url = $"https://cdn.example.com/catalog/product-000001/gloves-{i}.jpg",
            RelativeUrl = $"/catalog/product-000001/gloves-{i}.jpg",
            Group = "images",
            TypeId = "Image",
            AltText = $"Nitrile coated safety gloves, view {i}",
            Description = $"Product photo number {i}",
            LanguageCode = i % 2 == 0 ? "en-US" : null,
            SortOrder = i,
        }).ToList();
    }

    private static List<Asset> CreateAssets()
    {
        return Enumerable.Range(0, 3).Select(i => new Asset
        {
            Id = $"asset-{i:D6}",
            Name = $"datasheet-{i}.pdf",
            Url = $"https://cdn.example.com/catalog/product-000001/datasheet-{i}.pdf",
            RelativeUrl = $"/catalog/product-000001/datasheet-{i}.pdf",
            Group = "documents",
            TypeId = "File",
            MimeType = "application/pdf",
            Size = 120_000 + i,
            LanguageCode = i == 0 ? "en-US" : null,
            SortOrder = i,
        }).ToList();
    }

    private static List<Outline> CreateOutlines()
    {
        return Enumerable.Range(0, 2).Select(o => new Outline
        {
            Items = Enumerable.Range(0, 3 + o % 2).Select(d => new OutlineItem
            {
                Id = d == 0 ? "catalog-id" : $"category-{o}-{d}",
                SeoObjectType = d == 0 ? "Catalog" : "Category",
                SeoInfos = d == 0 ? [] : CreateSeoInfos($"category-{o}-{d}", $"safety/gloves/level-{o}-{d}").Take(1).ToList(),
            }).ToList(),
        }).ToList();
    }

    private static List<SeoInfo> CreateSeoInfos(string objectId, string semanticUrl)
    {
        return new[] { "en-US", "de-DE" }.Select(language => new SeoInfo
        {
            Id = $"seo-{objectId}-{language}",
            SemanticUrl = $"{semanticUrl}-{language}",
            LanguageCode = language,
            Name = $"{objectId} {language}",
            PageTitle = $"Buy {objectId} online",
            MetaDescription = $"Order {objectId} with fast delivery and volume discounts.",
            IsActive = true,
        }).ToList();
    }

    private static List<EditorialReview> CreateReviews()
    {
        // ~400 characters of content, as a rendered description is.
        var content = string.Concat(Enumerable.Repeat("Nitrile coated work gloves with a seamless knit liner for dexterity and breathability. ", 4));

        return new[] { "Main", "Short" }.Select((reviewType, i) => new EditorialReview
        {
            Id = $"review-{i:D6}",
            ReviewType = reviewType,
            LanguageCode = "en-US",
            Content = content,
        }).ToList();
    }

    private static List<CategoryLink> CreateLinks()
    {
        return Enumerable.Range(0, 2).Select(i => new CategoryLink
        {
            CatalogId = "catalog-id",
            CategoryId = $"virtual-category-{i}",
            ListEntryId = "product-000001",
            ListEntryType = "product",
            Priority = i,
        }).ToList();
    }
}
