namespace VirtoCommerce.XCatalog.Core
{
    public static class ModuleConstants
    {
        public const string KeyProperty = "KeyProperty";

        // Key under which one request pins the instant its validity-window filters are evaluated at. The
        // request-scoped cache is shared with every other consumer in the request, hence the prefix.
        // Deliberately not per-handler: product and category search in one response must be filtered
        // against the SAME instant, or a page can show a product as active and its category as not.
        public const string CertainDateRequestCacheKey = "xcatalog:search:certain-date";

        public const string DefaultBrandPropertyName = "Brand";
        public const string BrandSeoType = "Brand";
        public const string BrandsSeoType = "Brands";
    }
}
