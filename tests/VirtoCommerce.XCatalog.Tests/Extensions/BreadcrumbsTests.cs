using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Seo.Core.Models;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Extensions;
using Xunit;
using static VirtoCommerce.CatalogModule.Core.Extensions.SeoExtensions;
using static VirtoCommerce.StoreModule.Core.ModuleConstants.Settings.SEO;

namespace VirtoCommerce.XCatalog.Tests.Extensions
{
    public class BreadcrumbsTests
    {
        [Theory]
        [InlineData(null, "c1/p1")]
        [InlineData("", "c1/p1")]
        [InlineData("c1", "c1/p1")]
        [InlineData("c2", "c2/c21/p1")]
        [InlineData("c0/p2", "c1/p1")]
        [InlineData("c1/p2", "c1/p1")]
        [InlineData("c2/p2", "c2/c21/p1")]
        [InlineData("c1/c11", "c1/c11/p1")]
        [InlineData("c1/c12", "c1/c12/p1")]
        [InlineData("c2/c21", "c2/c21/p1")]
        [InlineData("c2/c22", "c2/c22/p1")]
        [InlineData("c1/c12/p2", "c1/c12/p1")]
        [InlineData("c2/c22/p2", "c2/c22/p1")]
        public void GetBestOutlinePath(string previousOutlinePath, string expectedOutlinePath)
        {
            // Arrange
            var store = GetStore("StoreId", "s1");
            var outlines = CreateOutlines(SeoProduct, "p1", "catalog", store, "s1/c1", "s1/c1/c11", "s1/c1/c12", "s1/c2/c21", "s1/c2/c22");

            // Act
            var actualOutlinePath = outlines.GetBestMatchingOutlinePath(store.Catalog, previousOutlinePath);

            // Assert
            Assert.Equal(expectedOutlinePath, actualOutlinePath);
        }

        [Theory]
        [InlineData(null, "c1/p2", "c1/p1", "c1")]
        [InlineData("cc", "c1/p2", "c1/p1", "s1")]
        public void GetBreadcrumbs_WhenMissingCatalogSeo_FirstItemShouldBeCategory(string catalogSemanticUrl, string previousOutlinePath, string expectedOutlinePath, string expectedFirstBreadcrumbId)
        {
            // Arrange
            var store = GetStore("StoreId", "s1");
            var outlines = CreateOutlines(SeoProduct, "p1", catalogSemanticUrl, store, "s1/c1", "s1/c1/c11", "s1/c1/c12", "s1/c2/c21", "s1/c2/c22");

            // Act
            var actualOutlinePath = outlines.GetBestMatchingOutlinePath(store.Catalog, previousOutlinePath);
            var actualBreadcrumbs = outlines.GetBreadcrumbs(store, store.DefaultLanguage, previousOutlinePath);

            // Assert
            Assert.Equal(expectedOutlinePath, actualOutlinePath);

            var actualFirstBreadcrumbId = actualBreadcrumbs.First().ItemId;
            Assert.Equal(expectedFirstBreadcrumbId, actualFirstBreadcrumbId);
        }

        private static Store GetStore(string storeId, string catalogId)
        {
            return new Store
            {
                Id = storeId,
                Catalog = catalogId,
                DefaultLanguage = "en-US",
                Languages = ["en-US", "de-DE"],
                Settings = [new ObjectSettingEntry { Name = SeoLinksType.Name, Value = SeoCollapsed }],
            };
        }

        private static List<Outline> CreateOutlines(string objectType, string objectId, string catalogSemanticUrl, Store store, params string[] parentPaths)
        {
            return parentPaths
                .Select(parentPath =>
                {
                    var items = parentPath
                        .Split('/')
                        .Append(objectId)
                        .Select(slug => new OutlineItem
                        {
                            Id = slug,
                            SeoObjectType = SeoCategory,
                            SeoInfos =
                            [
                                new SeoInfo
                                {
                                    IsActive = true,
                                    SemanticUrl = slug,
                                    StoreId = store.Id,
                                    LanguageCode = store.DefaultLanguage,
                                },
                            ],
                        })
                        .ToList();

                    items[0].SeoObjectType = SeoCatalog;

                    if (catalogSemanticUrl is null)
                    {
                        items[0].SeoInfos = null;
                    }
                    else
                    {
                        items[0].SeoInfos[0].SemanticUrl = catalogSemanticUrl;
                    }

                    items.Last().SeoObjectType = objectType;

                    return new Outline
                    {
                        Items = items,
                    };
                })
                .ToList();
        }
    }
}
