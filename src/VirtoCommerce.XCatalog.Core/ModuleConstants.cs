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
            public static SettingDescriptor TodayAvailabilityNote { get; } = new()
            {
                Name = "Catalog.TodayAvailabilityNote",
                GroupName = "Catalog|Pickup Location Settings",
                ValueType = SettingValueType.ShortText,
                IsLocalizable = true,
                IsDictionary = true,
                IsPublic = true,
            };

            public static SettingDescriptor TransferAvailabilityNote { get; } = new()
            {
                Name = "Catalog.TransferAvailabilityNote",
                GroupName = "Catalog|Pickup Location Settings",
                ValueType = SettingValueType.ShortText,
                IsLocalizable = true,
                IsDictionary = true,
                IsPublic = true,
            };

            public static SettingDescriptor GlobalTransferAvailabilityNote { get; } = new()
            {
                Name = "Catalog.GlobalTransferAvailabilityNote",
                GroupName = "Catalog|Pickup Location Settings",
                ValueType = SettingValueType.ShortText,
                IsLocalizable = true,
                IsDictionary = true,
                IsPublic = true,
            };

            public static SettingDescriptor GlobalTransferEnabled { get; } = new()
            {
                Name = "Catalog.GlobalTransferEnabled",
                GroupName = "Catalog|Pickup Location Settings",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
                IsPublic = true,
            };

            public static IEnumerable<SettingDescriptor> PickupLocationSettings
            {
                get
                {
                    yield return TodayAvailabilityNote;
                    yield return TransferAvailabilityNote;
                    yield return GlobalTransferAvailabilityNote;
                    yield return GlobalTransferEnabled;
                }
            }
        }
    }
}
