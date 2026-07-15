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

public class GetBrandQueryBuilder : QueryBuilder<GetBrandQuery, BrandAggregate, BrandType>
{
    protected override string Name => "brand";

    public GetBrandQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }

    protected override Task BeforeMediatorSend(IResolveFieldContext<object> context, GetBrandQuery request)
    {
        context.CopyArgumentsToUserContext();
        return base.BeforeMediatorSend(context, request);
    }
}
