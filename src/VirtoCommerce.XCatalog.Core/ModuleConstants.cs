namespace VirtoCommerce.XCatalog.Core
{
    public static class ModuleConstants
    {
        public const string KeyProperty = "KeyProperty";

        // Prefixed because the request-scoped cache is shared with every consumer in the request, and named
        // for the purpose rather than for the handler so a second validity-window consumer shares the instant
        // instead of reading its own clock: two searches in one response evaluated against different instants
        // can only produce an inconsistent view.
        public const string CertainDateRequestCacheKey = "xcatalog:search:certain-date";

        public const string DefaultBrandPropertyName = "Brand";
        public const string BrandSeoType = "Brand";
        public const string BrandsSeoType = "Brands";
    }
}
