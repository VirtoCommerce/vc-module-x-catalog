using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Builders;
using GraphQL.Resolvers;
using GraphQL.Types;
using MediatR;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas;
using static VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Data.Schemas;

public class InventorySchema : ISchemaBuilder
{
    public InventorySchema()
    {
    }

    [Obsolete("Use the constructor without IMediator. The mediator is resolved from context.RequestServices per request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
    public InventorySchema(IMediator mediator)
        : this()
    {
    }

    public void Build(ISchema schema)
    {
        var filfillmentCenterField = new FieldType
        {
            Name = "fulfillmentCenter",
            Arguments = new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "id", Description = "ID of the Fulfillment Center" }
            ),
            Type = GraphTypeExtensionHelper.GetActualType<FulfillmentCenterType>(),
            Resolver = new FuncFieldResolver<object>(async context =>
            {
                context.CopyArgumentsToUserContext();

                var id = context.GetArgument<string>("id");
                var storeId = context.GetArgument<string>("storeId");

                var request = new GetFulfillmentCenterQuery
                {
                    Id = id,
                    StoreId = storeId,
                };
                var fulfillmentCenter = await context.GetMediator().Send(request);

                if (fulfillmentCenter is not null)
                {
                    context.SetExpandedObjectGraph(fulfillmentCenter);
                }

                return fulfillmentCenter;
            })
        };
        schema.Query.AddField(filfillmentCenterField);

        var fulfillmentCentersConnectionBuilder = GraphTypeExtensionHelper.CreateConnection<FulfillmentCenterType, object>("fulfillmentCenters")
            .PageSize(Connections.DefaultPageSize)
            .Argument<StringGraphType>("storeId", "Search FFCs attached to a store")
            .Argument<StringGraphType>("query", "Search FFC by name")
            .Argument<StringGraphType>("sort", "The sort expression")
            .Argument<ListGraphType<StringGraphType>>("fulfillmentCenterIds", "Filter by FFC IDs");

        fulfillmentCentersConnectionBuilder.ResolveAsync(ResolveFulfillmentCentersConnectionAsync);
        schema.Query.AddField(fulfillmentCentersConnectionBuilder.FieldType);
    }

    private static async Task<object> ResolveFulfillmentCentersConnectionAsync(IResolveConnectionContext<object> context)
    {
        context.CopyArgumentsToUserContext();

        var take = context.First ?? context.PageSize ?? Connections.DefaultPageSize;
        var skip = Convert.ToInt32(context.After ?? 0.ToString());
        var fulfillmentCenterIds = context.GetArgument<List<string>>("fulfillmentCenterIds");

        var query = new SearchFulfillmentCentersQuery();

        if (fulfillmentCenterIds.IsNullOrEmpty())
        {
            query.Skip = skip;
            query.Take = take;
            query.StoreId = context.GetArgument<string>("storeId");
            query.Sort = context.GetArgument<string>("sort");
            query.Query = context.GetArgument<string>("query");
        }
        else
        {
            query.FulfillmentCenterIds = fulfillmentCenterIds.ToArray();
            query.Take = fulfillmentCenterIds.Count;
        }

        var response = await context.GetMediator().Send(query);

        return new PagedConnection<FulfillmentCenter>(response.Results, query.Skip, query.Take, response.TotalCount);
    }
}
