using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.InventoryModule.Core.Model;

namespace VirtoCommerce.XCatalog.Core.Queries
{
    public class GetFulfillmentCenterQuery : IQuery<FulfillmentCenter>
    {
        public string Id { get; set; }

        public string StoreId { get; set; }
    }
}
