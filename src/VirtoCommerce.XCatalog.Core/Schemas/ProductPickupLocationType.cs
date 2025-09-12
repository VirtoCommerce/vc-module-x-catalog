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

            Field(x => x.Name, nullable: true);
            Field(x => x.Address, nullable: true);
            Field<ProductPickupAvailabilityType>("AvailabilityType").Resolve(context => context.Source.AvailabilityType);
            Field(x => x.Note, nullable: true);
            Field(x => x.AvailableQuantity, nullable: true);
        }
    }
}
