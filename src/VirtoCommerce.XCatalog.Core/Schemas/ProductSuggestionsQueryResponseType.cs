using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas;

public class ProductSuggestionsQueryResponseType : ExtendableGraphType<ProductSuggestionsQueryResponse>
{
    public ProductSuggestionsQueryResponseType()
    {
        Field<ListGraphType<StringGraphType>>(nameof(ProductSuggestionsQueryResponse.Suggestions))
            .Resolve(context => context.Source.Suggestions);
    }
}
