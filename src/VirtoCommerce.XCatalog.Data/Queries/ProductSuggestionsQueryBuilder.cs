using System;
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

    public ProductSuggestionsQueryBuilder(IAuthorizationService authorizationService, IStoreService storeService)
        : base(authorizationService)
    {
        _storeService = storeService;
    }

    [Obsolete("Use the constructor without IMediator. The mediator is resolved from context.RequestServices per request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
    public ProductSuggestionsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService, IStoreService storeService)
        : this(authorizationService, storeService)
    {
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
