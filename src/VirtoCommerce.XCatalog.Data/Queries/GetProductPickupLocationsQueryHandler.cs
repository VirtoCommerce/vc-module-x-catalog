using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.InventoryModule.Core.Model.Search;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.ShippingModule.Core.Model.Search;
using VirtoCommerce.ShippingModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Services;
using ShippingConstants = VirtoCommerce.ShippingModule.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetProductPickupLocationsQueryHandler(
    IItemService itemService,
    IOptionalDependency<IProductInventorySearchService> productInventorySearchService,
    IOptionalDependency<IShippingMethodsSearchService> shippingMethodsSearchService,
    IOptionalDependency<IPickupLocationSearchService> pickupLocationSearchService,
    IStoreService storeService,
    ILocalizableSettingService localizableSettingService)
    : IQueryHandler<SearchProductPickupLocationsQuery, ProductPickupLocationSearchResult>
{
    public async Task<ProductPickupLocationSearchResult> Handle(SearchProductPickupLocationsQuery request, CancellationToken cancellationToken)
    {
        var store = await storeService.GetNoCloneAsync(request.StoreId);
        if (store == null)
        {
            throw new InvalidOperationException($"Store with id {request.StoreId} not found");
        }

        var product = await itemService.GetNoCloneAsync(request.ProductId);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with id {request.ProductId} not found");
        }

        var result = AbstractTypeFactory<ProductPickupLocationSearchResult>.TryCreateInstance();

        if (await IsPickupInStoreEnabled(request))
        {
            var pickupLocations = await SearchProductPickupLocations(request);

            var productInventories = await SearchProductInventoriesAsync(request);

            var resultItems = new List<ProductPickupLocation>();

            foreach (var pickupLocation in pickupLocations)
            {
                var pickupLocationProductInventories = productInventories
                    .Where(x => x.FulfillmentCenterId == pickupLocation.FulfillmentCenterId || pickupLocation.TransferFulfillmentCenterIds.Contains(x.FulfillmentCenterId))
                    .ToList();

                var productPickupLocation = await GetProductPickupLocationAsync(store, product, pickupLocation, pickupLocationProductInventories, request.CultureName);
                if (productPickupLocation != null)
                {
                    resultItems.Add(productPickupLocation);
                }
            }

            result.TotalCount = resultItems.Count;
            result.Results = ApplySort(resultItems, request).Skip(request.Skip).Take(request.Take).ToList();
        }

        return result;
    }

    protected virtual async Task<bool> IsPickupInStoreEnabled(SearchProductPickupLocationsQuery request)
    {
        if (shippingMethodsSearchService.Value == null)
        {
            return false;
        }

        var shippingMethodsSearchCriteria = AbstractTypeFactory<ShippingMethodsSearchCriteria>.TryCreateInstance();
        shippingMethodsSearchCriteria.StoreId = request.StoreId;
        shippingMethodsSearchCriteria.IsActive = true;
        shippingMethodsSearchCriteria.Codes = [ShippingConstants.BuyOnlinePickupInStoreShipmentCode];
        shippingMethodsSearchCriteria.Skip = 0;
        shippingMethodsSearchCriteria.Take = 1;

        return (await shippingMethodsSearchService.Value.SearchNoCloneAsync(shippingMethodsSearchCriteria)).TotalCount > 0;
    }

    protected virtual async Task<IList<PickupLocation>> SearchProductPickupLocations(SearchProductPickupLocationsQuery request)
    {
        if (pickupLocationSearchService.Value == null)
        {
            return new List<PickupLocation>();
        }

        var pickupLocationSearchCriteria = AbstractTypeFactory<PickupLocationSearchCriteria>.TryCreateInstance();
        pickupLocationSearchCriteria.StoreId = request.StoreId;
        pickupLocationSearchCriteria.IsActive = true;
        pickupLocationSearchCriteria.Keyword = request.Keyword;
        pickupLocationSearchCriteria.Sort = request.Sort;

        return await pickupLocationSearchService.Value.SearchAllNoCloneAsync(pickupLocationSearchCriteria);
    }

    protected virtual async Task<IList<InventoryInfo>> SearchProductInventoriesAsync(SearchProductPickupLocationsQuery request)
    {
        if (productInventorySearchService.Value == null)
        {
            return new List<InventoryInfo>();
        }

        var productInventorySearchCriteria = AbstractTypeFactory<ProductInventorySearchCriteria>.TryCreateInstance();
        productInventorySearchCriteria.ProductId = request.ProductId;

        return await productInventorySearchService.Value.SearchAllAsync(productInventorySearchCriteria, clone: false);
    }

    protected virtual async Task<ProductPickupLocation> GetProductPickupLocationAsync(Store store, CatalogProduct product, PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, string cultureName)
    {
        if (!product.TrackInventory.GetValueOrDefault())
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, null, ProductPickupAvailability.Today, cultureName);
        }

        var mainPickupLocationProductInventory = pickupLocationProductInventories
            .Where(x => x.FulfillmentCenterId == pickupLocation.FulfillmentCenterId)
            .Where(x => x.InStockQuantity > 0)
            .OrderByDescending(x => x.InStockQuantity)
            .FirstOrDefault();

        if (mainPickupLocationProductInventory != null)
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, mainPickupLocationProductInventory, ProductPickupAvailability.Today, cultureName);
        }

        var transferPickupLocationProductInventory = pickupLocationProductInventories
            .Where(x => pickupLocation.TransferFulfillmentCenterIds.Contains(x.FulfillmentCenterId))
            .Where(x => x.InStockQuantity > 0)
            .OrderByDescending(x => x.InStockQuantity)
            .FirstOrDefault();

        if (transferPickupLocationProductInventory != null)
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, transferPickupLocationProductInventory, ProductPickupAvailability.Transfer, cultureName);
        }

        if (store.Settings.GetValue<bool>(ModuleConstants.Settings.GlobalTransferEnabled))
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, null, ProductPickupAvailability.GlobalTransfer, cultureName);
        }

        return null;
    }

    protected virtual async Task<ProductPickupLocation> CreatePickupLocationFromProductInventoryAsync(PickupLocation pickupLocation, InventoryInfo productInventoryInfo, string productPickupAvailability, string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.Id = pickupLocation.Id;
        result.Name = pickupLocation.Name;
        result.Address = pickupLocation.Address?.ToString();
        result.GeoLocation = pickupLocation.GeoLocation;
        result.AvailabilityType = productPickupAvailability;
        result.AvailableQuantity = productInventoryInfo?.InStockQuantity;
        result.Note = await GetProductPickupLocationNoteAsync(productPickupAvailability, cultureName);

        return result;
    }

    protected virtual async Task<string> GetProductPickupLocationNoteAsync(string productPickupAvailability, string cultureName)
    {
        if (productPickupAvailability == ProductPickupAvailability.Today)
        {
            var result = (await localizableSettingService.GetValuesAsync(ModuleConstants.Settings.TodayAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(result))
            {
                result = "Today";
            }
            return result;
        }
        else if (productPickupAvailability == ProductPickupAvailability.Transfer)
        {
            var result = (await localizableSettingService.GetValuesAsync(ModuleConstants.Settings.TransferAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(result))
            {
                result = "Via transfer";
            }
            return result;
        }
        else if (productPickupAvailability == ProductPickupAvailability.GlobalTransfer)
        {
            var result = (await localizableSettingService.GetValuesAsync(ModuleConstants.Settings.GlobalTransferAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(result))
            {
                result = "Via transfer";
            }
            return result;
        }

        return null;
    }

    protected virtual IEnumerable<ProductPickupLocation> ApplySort(IList<ProductPickupLocation> items, SearchProductPickupLocationsQuery request)
    {
        if (request.Sort.IsNullOrEmpty())
        {
            return items
                .OrderBy(x => GetAvaiabilitySortOrder(x.AvailabilityType))
                .ThenByDescending(x => x.AvailableQuantity)
                .ThenBy(x => x.Name);
        }

        return items;
    }

    protected virtual int GetAvaiabilitySortOrder(string availabilityType)
    {
        return availabilityType switch
        {
            ProductPickupAvailability.Today => 10,
            ProductPickupAvailability.Transfer => 20,
            ProductPickupAvailability.GlobalTransfer => 30,
            _ => 100
        };
    }
}
