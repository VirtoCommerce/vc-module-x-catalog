using VirtoCommerce.XCatalog.Core.Extensions;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Extensions;
public class FormatExtensionsTests
{
    [Theory]
    [InlineData(10000.000000, "en-US", "10,000")]
    [InlineData(10000.000000, "ru-RU", "10 000")]
    [InlineData(10000.000000, "de-DE", "10.000")]
    [InlineData(10000.001000, "en-US", "10,000.001")]
    [InlineData(10000.001000, "ru-RU", "10 000,001")]
    [InlineData(10000.001000, "de-DE", "10.000,001")]
    public void Test(decimal value, string cultureName, string result)
    {
        var decimalString = value.FormatDecimal(cultureName);
        Assert.Equal(result, decimalString);
    }
}
