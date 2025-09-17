using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Schemas.ScalarTypes;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class ProductPickupLocationType : ExtendableGraphType<ProductPickupLocation>
    {
        public ProductPickupLocationType()
        {
            Name = "ProductPickupLocation";

            Field(x => x.Id, nullable: false);
            Field(x => x.Name, nullable: false);
            Field(x => x.Address, nullable: true);
            Field(x => x.GeoLocation, nullable: true);
            Field<ProductPickupAvailabilityType>("AvailabilityType").Resolve(context => context.Source.AvailabilityType);
            Field(x => x.Note, nullable: true);
            Field(x => x.AvailableQuantity, nullable: true);
        }
    }
}
