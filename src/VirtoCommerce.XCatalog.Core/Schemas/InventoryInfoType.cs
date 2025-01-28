using GraphQL.Types;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class InventoryInfoType : ExtendableGraphType<InventoryInfo>
    {
        public InventoryInfoType()
        {
            Name = "InventoryInfo";
            Description = "";

            Field<NonNullGraphType<LongGraphType>>("inStockQuantity")
                .Description("Inventory in stock quantity")
                .Resolve(context => context.Source.InStockQuantity);
            Field<NonNullGraphType<LongGraphType>>("reservedQuantity")
                .Description("Inventory reserved quantity")
                .Resolve(context => context.Source.ReservedQuantity);
            Field(d => d.FulfillmentCenterId);
            Field(d => d.FulfillmentCenterName);
            Field<NonNullGraphType<BooleanGraphType>>("allowPreorder")
                .Description("Allow preorder")
                .Resolve(context => context.Source.AllowPreorder);
            Field<NonNullGraphType<BooleanGraphType>>("allowBackorder")
                .Description("Allow backorder")
                .Resolve(context => context.Source.AllowBackorder);
            Field<DateTimeGraphType>("preorderAvailabilityDate")
                .Description("Preorder availability date")
                .Resolve(context => context.Source.PreorderAvailabilityDate);
            Field<DateTimeGraphType>("backorderAvailabilityDate")
                .Description("Backorder availability date")
                .Resolve(context => context.Source.BackorderAvailabilityDate);
        }
    }
}
