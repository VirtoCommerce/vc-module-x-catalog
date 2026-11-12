using System.Collections.Generic;
using FluentAssertions;
using Moq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Services;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Data.Middlewares;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Middlewares;

public class EvalProductsVendorMiddlewareTests
{
    [Fact]
    public void UpdateVendorsInProducts_MatchingVendor_UsesIXapiMapper()
    {
        // ToExpVendor used to be XCatalogMapper's own copy of the Member -> ExpVendor conversion;
        // it now lives (and is tested) only in x-api's IXapiMapper - this middleware just calls it.
        var product = new ExpProduct { IndexedProduct = new CatalogProduct { Vendor = "vendor-1" } };
        var member = new Contact { Id = "vendor-1" };
        var expected = new ExpVendor { Id = "vendor-1" };
        var xapiMapperMock = new Mock<IXapiMapper>();
        xapiMapperMock.Setup(x => x.ToExpVendor(member)).Returns(expected);
        var middleware = new TestableEvalProductsVendorMiddleware(xapiMapperMock.Object, Mock.Of<IMemberService>());

        middleware.CallUpdateVendorsInProducts([product], new Dictionary<string, Member> { ["vendor-1"] = member });

        product.Vendor.Should().BeSameAs(expected);
        xapiMapperMock.Verify(x => x.ToExpVendor(member), Times.Once);
    }

    [Fact]
    public void UpdateVendorsInProducts_NoVendorsResolved_LeavesProductsUntouched()
    {
        var product = new ExpProduct { IndexedProduct = new CatalogProduct { Vendor = "vendor-1" } };
        var xapiMapperMock = new Mock<IXapiMapper>();
        var middleware = new TestableEvalProductsVendorMiddleware(xapiMapperMock.Object, Mock.Of<IMemberService>());

        middleware.CallUpdateVendorsInProducts([product], new Dictionary<string, Member>());

        product.Vendor.Should().BeNull();
        xapiMapperMock.Verify(x => x.ToExpVendor(It.IsAny<Member>()), Times.Never);
    }

    private sealed class TestableEvalProductsVendorMiddleware(IXapiMapper mapper, IMemberService memberService)
        : EvalProductsVendorMiddleware(mapper, memberService)
    {
        public void CallUpdateVendorsInProducts(IList<ExpProduct> products, IDictionary<string, Member> vendorsByIds) =>
            UpdateVendorsInProducts(products, vendorsByIds);
    }
}
