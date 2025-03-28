using GraphQL;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Extensions
{
    public static class ResolveFieldContextExtensions
    {
        public static T GetCatalogQuery<T>(this IResolveFieldContext context) where T : ICatalogQuery
        {
            var result = AbstractTypeFactory<T>.TryCreateInstance();
            result.StoreId = context.GetArgumentOrValue<string>("storeId");
            result.UserId = context.GetArgumentOrValue<string>("userId") ?? context.GetCurrentUserId();
            result.OrganizationId = context.GetCurrentOrganizationId();
            result.CurrencyCode = context.GetArgumentOrValue<string>("currencyCode");
            result.CultureName = context.GetArgumentOrValue<string>("cultureName");
            result.PreviousBreadcrumbsPath = context.GetArgumentOrValue<string>("previousBreadcrumbsPath");

            return result;
        }

        public static void SetCatalogQuery(this IResolveFieldContext context, ICatalogQuery query)
        {
            context.UserContext["storeId"] = query.StoreId;
            context.UserContext["userId"] = query.UserId;
            context.UserContext["currencyCode"] = query.CurrencyCode;
            context.UserContext["cultureName"] = query.CultureName;
        }
    }
}
