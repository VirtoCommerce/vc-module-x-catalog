using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class ProductSuggestionsQueryBuilder : QueryBuilder<ProductSuggestionsQuery, ProductSuggestionsQueryResponse, ProductSuggestionsQueryResponseType>
{
    protected override string Name => "ProductSuggestions";

    public ProductSuggestionsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
