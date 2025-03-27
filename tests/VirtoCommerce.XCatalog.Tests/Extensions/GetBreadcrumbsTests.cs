using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CoreModule.Core.Outlines;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Extensions;
using Xunit;
using static VirtoCommerce.CatalogModule.Core.Extensions.SeoExtensions;
using static VirtoCommerce.StoreModule.Core.ModuleConstants.Settings.SEO;

namespace VirtoCommerce.XCatalog.Tests.Extensions
{
    public class GetBreadcrumbsTests
    {
        [Theory]
        [InlineData(null, "s1/c1/p1")]
        [InlineData("", "s1/c1/p1")]
        [InlineData("s1", "s1/c1/p1")]
        [InlineData("s1/c1", "s1/c1/p1")]
        [InlineData("s1/c2", "s1/c2/c21/p1")]
        [InlineData("s1/c0/p2", "s1/c1/p1")]
        [InlineData("s1/c1/p2", "s1/c1/p1")]
        [InlineData("s1/c2/p2", "s1/c2/c21/p1")]
        [InlineData("s1/c1/c11", "s1/c1/c11/p1")]
        [InlineData("s1/c1/c12", "s1/c1/c12/p1")]
        [InlineData("s1/c2/c21", "s1/c2/c21/p1")]
        [InlineData("s1/c2/c22", "s1/c2/c22/p1")]
        [InlineData("s1/c1/c12/p2", "s1/c1/c12/p1")]
        [InlineData("s1/c2/c22/p2", "s1/c2/c22/p1")]
        [InlineData("s2/c2", "s1/c1/p1")]
        public void GetBreadcrumbs(string previousBreadcrumbsPath, string expectedBreadcrumbsPath)
        {
            // Arrange
            var store = GetStore("StoreId", "s1");
            var outlines = CreateOutlines(SeoProduct, "p1", "catalog", store, "s1/c1", "s1/c1/c11", "s1/c1/c12", "s1/c2/c21", "s1/c2/c22");

            // Act
            var actualBreadcrumbs = outlines.GetBreadcrumbs(store, store.DefaultLanguage, previousBreadcrumbsPath);

            // Assert
            Assert.Equal(expectedBreadcrumbsPath, actualBreadcrumbs.Path);
        }

        [Theory]
        [InlineData(null, "s1/c1/p2", "s1/c1/p1", "c1")]
        [InlineData("cc", "s1/c1/p2", "s1/c1/p1", "s1")]
        public void GetBreadcrumbs_WhenMissingCatalogSeo_FirstItemShouldBeCategory(string catalogSemanticUrl, string previousBreadcrumbsPath, string expectedBreadcrumbsPath, string expectedFirstItemId)
        {
            // Arrange
            var store = GetStore("StoreId", "s1");
            var outlines = CreateOutlines(SeoProduct, "p1", catalogSemanticUrl, store, "s1/c1", "s1/c1/c11", "s1/c1/c12", "s1/c2/c21", "s1/c2/c22");

            // Act
            var actualBreadcrumbs = outlines.GetBreadcrumbs(store, store.DefaultLanguage, previousBreadcrumbsPath);

            // Assert
            Assert.Equal(expectedBreadcrumbsPath, actualBreadcrumbs.Path);

            var actualFirstItemId = actualBreadcrumbs.Items.First().ItemId;
            Assert.Equal(expectedFirstItemId, actualFirstItemId);
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
