using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Services;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetProductPickupLocationsQueryHandler(
    IItemService itemService,
    ICatalogPickupLocationService catalogPickupLocationService,
    IStoreService storeService)
    : IQueryHandler<SearchProductPickupLocationsQuery, ProductPickupLocationSearchResult>
{
    public async Task<ProductPickupLocationSearchResult> Handle(SearchProductPickupLocationsQuery request, CancellationToken cancellationToken)
    {
        var store = await storeService.GetNoCloneAsync(request.StoreId);
        if (store == null)
        {
            throw new InvalidOperationException($"Store with id {request.StoreId} not found");
        }

        var result = AbstractTypeFactory<ProductPickupLocationSearchResult>.TryCreateInstance();

        if (await catalogPickupLocationService.IsPickupInStoreEnabledAsync(request.StoreId))
        {
            var globalTransferEnabled = catalogPickupLocationService.GlobalTransferEnabled(store);

            var product = await itemService.GetNoCloneAsync(request.ProductId);
            if (product == null)
            {
                throw new InvalidOperationException($"Product with id {request.ProductId} not found");
            }

            var pickupLocations = await catalogPickupLocationService.SearchProductPickupLocationsAsync(request.StoreId, request.Keyword);

            var productInventories = await catalogPickupLocationService.SearchProductInventoriesAsync([request.ProductId]);

            var resultItems = new List<ProductPickupLocation>();

            foreach (var pickupLocation in pickupLocations)
            {
                var pickupLocationProductInventories = productInventories
                    .Where(x => x.FulfillmentCenterId == pickupLocation.FulfillmentCenterId || pickupLocation.TransferFulfillmentCenterIds.Contains(x.FulfillmentCenterId))
                    .ToList();

                var productPickupLocation = await GetProductPickupLocationAsync(product, pickupLocation, pickupLocationProductInventories, request.CultureName, globalTransferEnabled);
                if (productPickupLocation != null)
                {
                    resultItems.Add(productPickupLocation);
                }
            }

            result.TotalCount = resultItems.Count;
            result.Results = catalogPickupLocationService.ApplySort(resultItems, request.Sort).Skip(request.Skip).Take(request.Take).ToList();
        }

        return result;
    }

    protected virtual async Task<ProductPickupLocation> GetProductPickupLocationAsync(CatalogProduct product, PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, string cultureName, bool globalTransferEnabled)
    {
        if (!product.TrackInventory.GetValueOrDefault())
        {
            return await catalogPickupLocationService.CreatePickupLocationFromProductInventoryAsync(pickupLocation, null, ProductPickupAvailability.Today, cultureName);
        }

        var mainPickupLocationProductInventory = catalogPickupLocationService.GetMainPickupLocationProductInventory(pickupLocation, pickupLocationProductInventories, order: true);
        if (mainPickupLocationProductInventory != null)
        {
            return await catalogPickupLocationService.CreatePickupLocationFromProductInventoryAsync(pickupLocation, mainPickupLocationProductInventory, ProductPickupAvailability.Today, cultureName);
        }

        var transferPickupLocationProductInventory = catalogPickupLocationService.GetTransferPickupLocationProductInventory(pickupLocation, pickupLocationProductInventories, order: true);
        if (transferPickupLocationProductInventory != null)
        {
            return await catalogPickupLocationService.CreatePickupLocationFromProductInventoryAsync(pickupLocation, transferPickupLocationProductInventory, ProductPickupAvailability.Transfer, cultureName);
        }

        if (globalTransferEnabled)
        {
            return await catalogPickupLocationService.CreatePickupLocationFromProductInventoryAsync(pickupLocation, null, ProductPickupAvailability.GlobalTransfer, cultureName);
        }

        return null;
    }
}
