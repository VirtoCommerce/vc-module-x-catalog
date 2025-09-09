using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class ProductPickupLocationType : ExtendableGraphType<ProductPickupLocation>
    {
        public ProductPickupLocationType()
        {
            Name = "PickupLocation";

            Field(x => x.Name, nullable: true);
            Field(x => x.Address, nullable: true);
            Field(x => x.ShipmentType, nullable: true);
            Field(x => x.ShipmentHours, nullable: true);
            Field(x => x.AvailableQuantity, nullable: true);
        }
    }
}
