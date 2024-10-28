using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries;

public class GetProductConfigurationQuery : CatalogQueryBase<ConfigurationQueryResponse>
{
    public string ProductId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(ProductId));
    }
    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);

        ProductId = context.GetArgument<string>(nameof(ProductId));
    }
}
