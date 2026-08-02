namespace VirtoCommerce.XCatalog.Core
{
    public static class ModuleConstants
    {
        public const string KeyProperty = "KeyProperty";

        // Key under which one request pins the instant its validity-window filters are evaluated at. The
        // request-scoped cache is shared with every other consumer in the request, hence the prefix.
        // Named for the purpose rather than for the handler so that a second validity-window consumer can
        // share the instant: two searches in one response evaluated against different instants can only
        // produce an inconsistent view. Today product search is the only consumer - category search does not
        // filter by date at all, and the pricing middleware still reads its own clock.
        public const string CertainDateRequestCacheKey = "xcatalog:search:certain-date";

        public const string DefaultBrandPropertyName = "Brand";
        public const string BrandSeoType = "Brand";
        public const string BrandsSeoType = "Brands";
    }
}
