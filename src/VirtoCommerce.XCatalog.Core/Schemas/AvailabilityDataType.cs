using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class AvailabilityDataType : ExtendableGraphType<ExpAvailabilityData>
    {
        public AvailabilityDataType()
        {
            Name = "AvailabilityData";

            Field(x => x.AvailableQuantity, nullable: false).Description("Available quantity");
            Field<NonNullGraphType<BooleanGraphType>>("IsBuyable")
                .Description("Is buyable")
                .Resolve(context => context.Source.IsBuyable);
            Field<NonNullGraphType<BooleanGraphType>>("IsAvailable")
                .Description("Is available")
                .Resolve(context => context.Source.IsAvailable);
            Field<NonNullGraphType<BooleanGraphType>>("IsInStock")
                .Description("Is in stock")
                .Resolve(context => context.Source.IsInStock);
            Field<NonNullGraphType<BooleanGraphType>>("IsActive")
                .Description("Is active")
                .Resolve(context => context.Source.IsActive);
            Field<NonNullGraphType<BooleanGraphType>>("IsTrackInventory")
                .Description("Is track inventory")
                .Resolve(context => context.Source.IsTrackInventory);
            Field<NonNullGraphType<BooleanGraphType>>("IsEstimated")
               .Description("Is estimated")
               .Resolve(context => context.Source.IsEstimated);

            ExtendableField<NonNullGraphType<ListGraphType<NonNullGraphType<InventoryInfoType>>>>("inventories",
                "Inventories",
                resolve: context => context.Source.InventoryAll);
        }
    }
}
