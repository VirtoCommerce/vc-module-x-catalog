using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetProductPickupLocationsQueryBuilder : SearchQueryBuilder<SearchProductPickupLocationsQuery, ProductPickupLocationSearchResult, ProductPickupLocation, ProductPickupLocationType>
{
    protected override string Name => "productPickupLocations";

    public GetProductPickupLocationsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
