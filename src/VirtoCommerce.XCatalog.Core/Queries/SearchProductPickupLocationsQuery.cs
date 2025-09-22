using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XPickup.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries;

public class SearchProductPickupLocationsQuery : SearchQuery<ProductPickupLocationSearchResult>
{
    public string ProductId { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(ProductId), description: "Product Id");
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId), description: "Store Id");
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(CultureName), description: "Culture name (\"en-US\")");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);

        ProductId = context.GetArgument<string>(nameof(ProductId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
    }
}
