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
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Data.Services;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetProductPickupLocationsQueryHandler(
    IItemService itemService,
    IProductInventorySearchService productInventorySearchService,
    IStoreService storeService,
    ILocalizableSettingService localizableSettingService)
    : IQueryHandler<SearchProductPickupLocationsQuery, ProductPickupLocationSearchResult>
{
    public async Task<ProductPickupLocationSearchResult> Handle(SearchProductPickupLocationsQuery request, CancellationToken cancellationToken)
    {
        var store = await storeService.GetByIdAsync(request.StoreId);

        if (store == null)
        {
            throw new InvalidOperationException($"Store with id {request.StoreId} not found");
        }

        var product = await itemService.GetByIdAsync(request.ProductId);

        if (product == null)
        {
            throw new InvalidOperationException($"Product with id {request.ProductId} not found");
        }

        var productInventories = await SearchProductInventoriesAsync(request);

        var resultItems = new List<ProductPickupLocation>();

        foreach (var productInventory in productInventories)
        {
            var pickupLocation = await GetPickupLocationAsync(store, product, productInventory, request.CultureName);

            if (pickupLocation != null)
            {
                resultItems.Add(pickupLocation);
            }
        }

        var result = AbstractTypeFactory<ProductPickupLocationSearchResult>.TryCreateInstance();

        result.TotalCount = resultItems.Count;
        result.Results = ApplySort(resultItems, request).Skip(request.Skip).Take(request.Take).ToList();

        return result;
    }

    protected virtual async Task<IList<InventoryInfo>> SearchProductInventoriesAsync(SearchProductPickupLocationsQuery request)
    {
        var productInventorySearchCriteria = AbstractTypeFactory<ProductInventorySearchCriteria>.TryCreateInstance();
        productInventorySearchCriteria.ProductId = request.ProductId;
        productInventorySearchCriteria.Sort = request.Sort;

        return await productInventorySearchService.SearchAllAsync(productInventorySearchCriteria, false);
    }

    protected virtual async Task<ProductPickupLocation> GetPickupLocationAsync(Store store, CatalogProduct product, InventoryInfo productInventory, string cultureName)
    {
        if (productInventory.FulfillmentCenterId == store.MainFulfillmentCenterId)
        {
            if (!product.TrackInventory.GetValueOrDefault())
            {
                return await CreateDefaultPickupLocationAsync(cultureName);
            }
            else if (productInventory.InStockQuantity > 0)
            {
                return await CreatePickupLocationFromProductInventoryAsync(productInventory, ProductPickupAvailability.Today, cultureName);
            }
            else if (store.Settings.GetValue<bool>(ModuleConstants.Settings.GlobalTransferEnabled))
            {
                return await CreateGlobalTransferPickupLocationAsync(cultureName);
            }
        }
        else if (store.AdditionalFulfillmentCenterIds.Contains(productInventory.FulfillmentCenterId))
        {
            if (product.TrackInventory.GetValueOrDefault() && productInventory.InStockQuantity > 0)
            {
                return await CreatePickupLocationFromProductInventoryAsync(productInventory, ProductPickupAvailability.Transfer, cultureName);
            }
            else if (store.Settings.GetValue<bool>(ModuleConstants.Settings.GlobalTransferEnabled))
            {
                return await CreateGlobalTransferPickupLocationAsync(cultureName);
            }
        }

        return null;
    }

    protected virtual async Task<ProductPickupLocation> CreateDefaultPickupLocationAsync(string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.AvailabilityType = ProductPickupAvailability.Today;
        result.Note = await GetProductPickupLocationNoteAsync(ProductPickupAvailability.Today, cultureName);

        return result;
    }

    protected virtual async Task<ProductPickupLocation> CreatePickupLocationFromProductInventoryAsync(InventoryInfo productInventoryInfo, string productPickupAvailability, string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.AvailabilityType = productPickupAvailability;
        result.Note = await GetProductPickupLocationNoteAsync(productPickupAvailability, cultureName);
        result.Name = productInventoryInfo.FulfillmentCenter.Name;
        result.Address = productInventoryInfo.FulfillmentCenter.Address.ToString();
        result.AvailableQuantity = productInventoryInfo.InStockQuantity;

        return result;
    }

    protected virtual async Task<ProductPickupLocation> CreateGlobalTransferPickupLocationAsync(string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.AvailabilityType = ProductPickupAvailability.GlobalTransfer;
        result.Note = await GetProductPickupLocationNoteAsync(ProductPickupAvailability.GlobalTransfer, cultureName);

        return result;
    }

    protected virtual async Task<string> GetProductPickupLocationNoteAsync(string productPickupAvailability, string cultureName)
    {
        if (productPickupAvailability == ProductPickupAvailability.Today)
        {
            return (await localizableSettingService.GetValuesAsync(ModuleConstants.Settings.TodayAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
        }
        else if (productPickupAvailability == ProductPickupAvailability.Transfer)
        {
            return (await localizableSettingService.GetValuesAsync(ModuleConstants.Settings.TransferAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
        }
        else if (productPickupAvailability == ProductPickupAvailability.GlobalTransfer)
        {
            return (await localizableSettingService.GetValuesAsync(ModuleConstants.Settings.GlobalTransferAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
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
