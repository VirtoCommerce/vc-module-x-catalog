using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Schemas;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetProductPickupLocationsQueryBuilder : SearchQueryBuilder<SearchProductPickupLocationsQuery, ProductPickupLocationSearchResult, ProductPickupLocation, ProductPickupLocationType>
{
    protected override string Name => "productPickupLocations";

    public GetProductPickupLocationsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
