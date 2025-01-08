using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class FulfillmentCenterType : ExtendableGraphType<FulfillmentCenter>
    {
        public FulfillmentCenterType(IOptionalDependency<IFulfillmentCenterGeoService> fulfillmentCenterGeoService)
        {
            Field(x => x.Id).Description("Fulfillment Center ID.");
            Field(x => x.Name, nullable: true).Description("Fulfillment Center name.");
            Field(x => x.Description, nullable: true).Description("Fulfillment Center description.");
            Field(x => x.OuterId, nullable: true).Description("Fulfillment Center outerId.");
            Field(x => x.GeoLocation, nullable: true).Description("Fulfillment Center GEO location.");
            Field(x => x.ShortDescription, nullable: true).Description("Fulfillment Center short description.");
            Field<FulfillmentCenterAddressType>(nameof(FulfillmentCenter.Address).ToCamelCase())
                .Description("Fulfillment Center address.")
                .Resolve(x => x.Source.Address);

            Field<ListGraphType<FulfillmentCenterType>>("nearest")
                .Arguments(new QueryArguments(new QueryArgument<IntGraphType> { Name = "take" }))
                .Description("Nearest Fulfillment Centers")
                .ResolveAsync(async context =>
                {
                    if (fulfillmentCenterGeoService.Value == null)
                    {
                        return new List<FulfillmentCenter>();
                    }

                    var take = context.GetArgument("take", 10);

                    var result = await fulfillmentCenterGeoService.Value.GetNearestAsync(context.Source.Id, take);
                    return result;
                });
        }
    }
}
