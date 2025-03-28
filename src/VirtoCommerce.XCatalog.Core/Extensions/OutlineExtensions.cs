using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL;
using VirtoCommerce.CatalogModule.Core.Extensions;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Outlines;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Extensions;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Extensions
{
    public static class OutlineExtensions
    {
        public static string GetBestSeoPath(this IList<Outline> outlines, Store store, string language, string previousBreadcrumbsPath)
        {
            var outline = outlines.GetBestMatchingOutline(store.Catalog, previousBreadcrumbsPath);

            return outline?.Items?.GetSeoPath(store, language);
        }

        public static string GetBestOutlinePath(this IList<Outline> outlines, string catalogId, string previousBreadcrumbsPath)
        {
            var outline = outlines.GetBestMatchingOutline(catalogId, previousBreadcrumbsPath);

            return outline?.Items?.GetOutlinePath();
        }

        public static string GetOutlinePath(this IEnumerable<OutlineItem> outlineItems)
        {
            var pathSegments = outlineItems
                ?.Where(x => !x.IsCatalog())
                .Select(x => x.Id)
                .ToList();

            if (pathSegments is null || pathSegments.Count == 0 || pathSegments.Any(x => x is null))
            {
                return null;
            }

            return string.Join('/', pathSegments);
        }

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

        [Obsolete("Use GetBreadcrumbs()", DiagnosticId = "VC0010", UrlFormat = "https://docs.virtocommerce.org/platform/user-guide/versions/virto3-products-versions/")]
        public static IEnumerable<Breadcrumb> GetBreadcrumbsFromOutLine(this IEnumerable<Outline> outlines, Store store, string cultureName)
        {
            return outlines.GetBreadcrumbs(store, cultureName).Items;
        }

        public static Breadcrumbs GetBreadcrumbs<TEntity>(this TEntity entity, IResolveFieldContext context)
            where TEntity : IEntity, IHasOutlines
        {
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(context);

            var contextKey = $"breadcrumbs_{entity.Id}";
            var breadcrumbs = context.GetValue<Breadcrumbs>(contextKey);

            if (breadcrumbs != null)
            {
                return breadcrumbs;
            }

            var store = context.GetValue<Store>("store");
            var cultureName = context.GetArgumentOrValue<string>("cultureName");
            var previousBreadcrumbsPath = context.GetArgumentOrValue<string>("previousBreadcrumbsPath");

            breadcrumbs = entity.Outlines.GetBreadcrumbs(store, cultureName, previousBreadcrumbsPath);
            context.UserContext[contextKey] = breadcrumbs;

            return breadcrumbs;
        }

        public static Breadcrumbs GetBreadcrumbs(this IEnumerable<Outline> outlines, Store store, string cultureName = null, string previousBreadcrumbsPath = null)
        {
            var outline = outlines.GetBestMatchingOutline(store.Catalog, previousBreadcrumbsPath);
            var items = outline?.GetBreadcrumbs(store, cultureName) ?? [];

            return new Breadcrumbs
            {
                Path = string.Join('/', items.Select(x => x.ItemId)),

                // Exclude catalog item if it has no SEO information
                Items = items.Where(x => !string.IsNullOrEmpty(x.SemanticUrl)).ToList(),
            };
        }

        public static Outline GetBestMatchingOutline(this IEnumerable<Outline> outlines, string catalogId, string previousBreadcrumbsPath)
        {
            var catalogOutlines = outlines?.Where(x => x.Items.ContainsCatalog(catalogId)).ToList();

            if (catalogOutlines is null || catalogOutlines.Count == 0)
            {
                return null;
            }

            return string.IsNullOrEmpty(previousBreadcrumbsPath)
                ? catalogOutlines.First()
                : catalogOutlines.GetBestMatchingOutline(previousBreadcrumbsPath);
        }


        private static Outline GetBestMatchingOutline(this List<Outline> outlines, string previousBreadcrumbsPath)
        {
            Outline bestOutline = null;
            var previousIds = previousBreadcrumbsPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (var (previousId, i) in previousIds.Select((x, i) => (x, i)))
            {
                var matchingOutlines = new List<Outline>();

                foreach (var outline in outlines.Where(x => x.Items.Count > i))
                {
                    var item = outline.Items is IList<OutlineItem> list
                        ? list[i]
                        : outline.Items.Skip(i).First();

                    if (item.Id.EqualsIgnoreCase(previousId))
                    {
                        matchingOutlines.Add(outline);
                    }
                }

                if (matchingOutlines.Count > 0)
                {
                    outlines = matchingOutlines;
                    bestOutline = matchingOutlines.First();
                }
                else
                {
                    break;
                }
            }

            return bestOutline ?? outlines.First();
        }

        private static List<Breadcrumb> GetBreadcrumbs(this Outline outline, Store store, string cultureName)
        {
            var breadcrumbs = new List<Breadcrumb>();
            var items = outline.Items?.ToList() ?? [];

            while (items.Count > 0)
            {
                var breadcrumb = items.GetBreadcrumbForLastItem(store, cultureName);

                if (breadcrumb != null)
                {
                    breadcrumbs.Insert(0, breadcrumb);
                }

                items.RemoveAt(items.Count - 1);
            }

            return breadcrumbs;
        }

        private static Breadcrumb GetBreadcrumbForLastItem(this List<OutlineItem> items, Store store, string cultureName)
        {
            var item = items.Last();
            var seo = item.GetBestMatchingSeoInfo(store, cultureName);
            var seoTitle = seo?.PageTitle.EmptyToNull();
            var semanticUrl = seo?.SemanticUrl.EmptyToNull();

            if (item.IsCatalog())
            {
                var catalogSemanticUrl = seo is null
                    ? null
                    : semanticUrl ?? "catalog";

                return new Breadcrumb(item.SeoObjectType)
                {
                    ItemId = item.Id,
                    Title = seoTitle ?? "Catalog",
                    SemanticUrl = catalogSemanticUrl,
                    SeoPath = catalogSemanticUrl,
                };
            }

            var seoPath = items.GetSeoPath(store, cultureName);

            return string.IsNullOrEmpty(seoPath)
                ? null
                : new Breadcrumb(item.SeoObjectType)
                {
                    ItemId = item.Id,
                    Title = seoTitle ?? item.LocalizedName?.GetValue(cultureName).EmptyToNull() ?? item.Name,
                    SemanticUrl = semanticUrl,
                    SeoPath = seoPath,
                };
        }

        [Obsolete("Use VirtoCommerce.StoreModule.Core.Extensions.GetBestMatchingSeoInfo()", DiagnosticId = "VC0010", UrlFormat = "https://docs.virtocommerce.org/platform/user-guide/versions/virto3-products-versions/")]
        public static SeoInfo SeoInfoForStoreAndLanguage(OutlineItem item, string storeId, string cultureName)
        {
            return item.SeoInfos?.FirstOrDefault(x => x.StoreId == storeId && x.LanguageCode == cultureName);
        }
    }
}
