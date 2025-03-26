using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CatalogModule.Core.Extensions;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Outlines;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Extensions
{
    public static class OutlineExtensions
    {
        /// <summary>
        /// Returns SEO path if all outline items of the first outline have SEO keywords, otherwise returns default value.
        /// Path: GrandParentCategory/ParentCategory/ProductCategory/Product
        /// </summary>
        /// <param name="outlines"></param>
        /// <param name="store"></param>
        /// <param name="language"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        [Obsolete("Use VirtoCommerce.CatalogModule.Core.Extensions", DiagnosticId = "VC0010", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions/")]
        public static string GetSeoPath(this IEnumerable<Outline> outlines, Store store, string language, string defaultValue)
        {
            return CatalogModule.Core.Extensions.OutlineExtensions.GetSeoPath(outlines, store, language, defaultValue);
        }

        /// <summary>
        /// Returns best matching outline path for the given catalog: CategoryId/CategoryId2.
        /// </summary>
        /// <param name="outlines"></param>
        /// <param name="catalogId"></param>
        /// <returns></returns>
        [Obsolete("Use VirtoCommerce.CatalogModule.Core.Extensions", DiagnosticId = "VC0010", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions/")]
        public static string GetOutlinePath(this IEnumerable<Outline> outlines, string catalogId)
        {
            return CatalogModule.Core.Extensions.OutlineExtensions.GetOutlinePath(outlines, catalogId);
        }

        /// <summary>
        /// Returns product's category outline.
        /// </summary>
        /// <param name="product"></param>
        /// <returns></returns>
        public static string GetCategoryOutline(this CatalogProduct product)
        {
            var result = string.Empty;

            if (!string.IsNullOrEmpty(product?.Outline))
            {
                var i = product.Outline.LastIndexOf('/');
                if (i >= 0)
                {
                    result = product.Outline.Substring(0, i);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns all concatenated relative outlines for the given catalog
        /// </summary>
        /// <param name="outlines"></param>
        /// <param name="catalogId"></param>
        /// <returns></returns>
        public static string GetOutlinePaths(this IEnumerable<Outline> outlines, string catalogId)
        {
            var relativePaths = outlines
                ?.Where(x => x.Items.ContainsCatalog(catalogId))
                .Select(x => x.ToCatalogRelativePath())
                .ToList();

            return relativePaths?.Count > 0
                ? string.Join(';', relativePaths)
                : string.Empty;
        }

        /// <summary>s
        /// Returns catalog's relative outline path
        /// </summary>
        /// <param name="outline"></param>
        /// <returns></returns>
        public static string ToCatalogRelativePath(this Outline outline)
        {
            return outline?.Items is null
                ? null
                : string.Join('/',
                outline.Items
                        .Where(x => x != null && !x.IsCatalog())
                        .Select(x => x.Id));
        }

        public static IEnumerable<Breadcrumb> GetBreadcrumbsFromOutLine(this IEnumerable<Outline> outlines, Store store, string cultureName)
        {
            var outlineItems = outlines
                ?.FirstOrDefault(outline => outline.Items != null && outline.Items.Any(item => item.Id == store.Catalog && item.SeoObjectType == "Catalog"))
                ?.Items
                .ToList();

            if (outlineItems.IsNullOrEmpty())
            {
                return Enumerable.Empty<Breadcrumb>();
            }

            var breadcrumbs = new List<Breadcrumb>();

#pragma warning disable S2259 // False positive by IsNullOrEmpty
            for (var i = outlineItems.Count - 1; i > 0; i--)
            {
                var item = outlineItems[i];

                var innerOutline = new List<Outline> { new Outline { Items = outlineItems } };
                var seoPath = innerOutline.GetSeoPath(store, cultureName);

                outlineItems.Remove(item);
                if (string.IsNullOrWhiteSpace(seoPath))
                {
                    continue;
                }

                var seoInfoForStoreAndLanguage = SeoInfoForStoreAndLanguage(item, store.Id, cultureName);

                var breadcrumb = new Breadcrumb(item.SeoObjectType)
                {
                    ItemId = item.Id,
                    Title = ResolveItemTitle(item, seoInfoForStoreAndLanguage, cultureName),
                    SemanticUrl = seoInfoForStoreAndLanguage?.SemanticUrl,
                    SeoPath = seoPath
                };
                breadcrumbs.Insert(0, breadcrumb);
            }
#pragma warning restore S2259 // Null pointers should not be dereferenced

            var catalog = outlineItems[0];
            var catalogSeoInfoForStoreAndLanguage = SeoInfoForStoreAndLanguage(catalog, store.Id, cultureName);
            if (catalog.SeoObjectType == "Catalog" && catalogSeoInfoForStoreAndLanguage != null)
            {
                var breadcrumb = new Breadcrumb(catalog.SeoObjectType)
                {
                    ItemId = catalog.Id,
                    Title = catalogSeoInfoForStoreAndLanguage.PageTitle?.EmptyToNull() ?? "Catalog",
                    SemanticUrl = catalogSeoInfoForStoreAndLanguage.SemanticUrl?.EmptyToNull() ?? "catalog",
                    SeoPath = catalogSeoInfoForStoreAndLanguage.SemanticUrl?.EmptyToNull() ?? "catalog"
                };
                breadcrumbs.Insert(0, breadcrumb);
            }

            return breadcrumbs;
        }

        private static string ResolveItemTitle(OutlineItem item, SeoInfo seoInfoForStoreAndLanguage, string cultureName)
        {
            var pageTitle = seoInfoForStoreAndLanguage?.PageTitle?.EmptyToNull();
            if (!string.IsNullOrEmpty(pageTitle))
            {
                return pageTitle;
            }

            if (item.LocalizedName != null && item.LocalizedName.TryGetValue(cultureName, out var localizedTitle))
            {
                return localizedTitle;
            }

            return item.Name;
        }

        public static SeoInfo SeoInfoForStoreAndLanguage(OutlineItem item, string storeId, string cultureName)
        {
            return item.SeoInfos?.FirstOrDefault(x => x.StoreId == storeId && x.LanguageCode == cultureName);
        }
    }
}
