using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class SearchBrandQueryBuilder : SearchQueryBuilder<SearchBrandQuery, SearchBrandResponse, BrandAggregate, BrandType>
{
    public SearchBrandQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }

    protected override string Name => "brands";

    protected override Task BeforeMediatorSend(IResolveFieldContext<object> context, SearchBrandQuery request)
    {
        context.CopyArgumentsToUserContext();
        return base.BeforeMediatorSend(context, request);
    }

    protected override Task AfterMediatorSend(IResolveFieldContext<object> context, SearchBrandQuery request, SearchBrandResponse response)
    {
        // Make the store available to StoreUrlType field resolution (banner/logo URLs).
        var store = response?.Results?.FirstOrDefault()?.Store;
        if (store != null)
        {
            context.UserContext["store"] = store;
        }

        return base.AfterMediatorSend(context, request, response);
    }
}
