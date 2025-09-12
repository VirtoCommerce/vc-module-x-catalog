using GraphQL.Types;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas.ScalarTypes;

public class ProductPickupAvailabilityType : EnumerationGraphType
{
    public ProductPickupAvailabilityType()
    {
        Add(ProductPickupAvailability.Today, value: ProductPickupAvailability.Today, description: "Available today (within hours)");
        Add(ProductPickupAvailability.Transfer, value: ProductPickupAvailability.Transfer, description: "Available via transfer (within days)");
        Add(ProductPickupAvailability.GlobalTransfer, value: ProductPickupAvailability.GlobalTransfer, description: "Available via global transfer (within weeks)");
    }
}
