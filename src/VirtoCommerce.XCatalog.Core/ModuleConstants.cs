using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.XCatalog.Core
{
    public static class ModuleConstants
    {
        public const string KeyProperty = "KeyProperty";

        public const string DefaultBrandPropertyName = "Brand";
        public const string BrandSeoType = "Brand";
        public const string BrandsSeoType = "Brands";

        public static class Settings
        {
            public static class General
            {
                public static SettingDescriptor BrandsEnabled { get; } = new()
                {
                    Name = "XCatalog.BrandsEnabled",
                    GroupName = "Catalog|Brands",
                    ValueType = SettingValueType.Boolean,
                    IsPublic = true,
                    DefaultValue = false,
                };

                public static IEnumerable<SettingDescriptor> AllGeneralSettings
                {
                    get
                    {
                        yield return BrandsEnabled;
                    }
                }
            }

            public static IEnumerable<SettingDescriptor> AllSettings
            {
                get
                {
                    return General.AllGeneralSettings;
                }
            }

            public static IEnumerable<SettingDescriptor> StoreLevelSettings
            {
                get
                {
                    yield return General.BrandsEnabled;
                }
            }
        }
    }
}
