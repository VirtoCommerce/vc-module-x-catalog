using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.XCatalog.Data.Extensions;

internal static class OverridableType<T> where T : new()
{
    //TODO: Move to AbstractTypeFactory<T> when it will be implemented
    public static T New() => AbstractTypeFactory<T>.HasOverrides ? AbstractTypeFactory<T>.TryCreateInstance() : new T();
}
