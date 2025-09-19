using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Services;

public interface ICatalogPickupLocationService
{
    Task<bool> IsPickupInStoreEnabledAsync(string storeId);

    Task<IList<PickupLocation>> SearchProductPickupLocationsAsync(string storeId, string keyword);

    Task<IList<InventoryInfo>> SearchProductInventoriesAsync(IList<string> productIds);

    InventoryInfo GetMainPickupLocationProductInventory(PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, long minQuantity, bool order);

    InventoryInfo GetTransferPickupLocationProductInventory(PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, long minQuantity, bool order);

    Task<ProductPickupLocation> CreatePickupLocationFromProductInventoryAsync(PickupLocation pickupLocation, InventoryInfo productInventoryInfo, string productPickupAvailability, string cultureName);

    bool GlobalTransferEnabled(Store store);

    IEnumerable<ProductPickupLocation> ApplySort(IList<ProductPickupLocation> items, string sort);
}
