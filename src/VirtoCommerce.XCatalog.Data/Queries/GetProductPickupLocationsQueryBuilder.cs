using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Schemas;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetProductPickupLocationsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
    : SearchQueryBuilder<SearchProductPickupLocationsQuery, ProductPickupLocationSearchResult, ProductPickupLocation, ProductPickupLocationType>(mediator, authorizationService)
{
    protected override string Name => "productPickupLocations";
}
