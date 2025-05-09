using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries;
public class GetBrandQuery : Query<BrandAggregate>
{
    public string Id { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(Id), description: "Brand Id");
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId), description: "Store Id");
        yield return Argument<StringGraphType>(nameof(CultureName), description: "Currency code (\"USD\")");
    }

    public override void Map(IResolveFieldContext context)
    {
        Id = context.GetArgument<string>(nameof(Id));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
    }
}
