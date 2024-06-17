using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries
{
    internal class GetFulfillmentCenterQueryHandler : IQueryHandler<GetFulfillmentCenterQuery, FulfillmentCenter>
    {
        private readonly IFulfillmentCenterService _fulfillmentCenterService;

        public GetFulfillmentCenterQueryHandler(IFulfillmentCenterService fulfillmentCenterService)
        {
            _fulfillmentCenterService = fulfillmentCenterService;
        }

        public async Task<FulfillmentCenter> Handle(GetFulfillmentCenterQuery request, CancellationToken cancellationToken)
        {
            var fulfillmentCenter = await _fulfillmentCenterService.GetByIdAsync(request.Id);

            return fulfillmentCenter;
        }
    }
}
