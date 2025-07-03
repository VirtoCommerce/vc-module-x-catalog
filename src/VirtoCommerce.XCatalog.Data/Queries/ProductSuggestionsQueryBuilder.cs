using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.XCatalog.Core.Authorization;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class ProductSuggestionsQueryBuilder : QueryBuilder<ProductSuggestionsQuery, ProductSuggestionsQueryResponse, ProductSuggestionsQueryResponseType>
{
    protected override string Name => "ProductSuggestions";

    private readonly IStoreService _storeService;

    public ProductSuggestionsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService, IStoreService storeService)
        : base(mediator, authorizationService)
    {
        _storeService = storeService;
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, ProductSuggestionsQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        if (!string.IsNullOrEmpty(request.StoreId))
        {
            var store = await _storeService.GetByIdAsync(request.StoreId);
            request.Store = store;
        }

        await Authorize(context, request.Store, new CanAccessStoreAuthorizationRequirement());
    }
}
