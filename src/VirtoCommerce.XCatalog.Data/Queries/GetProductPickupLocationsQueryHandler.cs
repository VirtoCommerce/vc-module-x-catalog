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
        var result = AbstractTypeFactory<ProductPickupLocationSearchResult>.TryCreateInstance();

        var store = await storeService.GetByIdAsync(request.StoreId);

        if (store == null)
        {
            throw new InvalidOperationException($"Store with id {request.StoreId} not found");//TODO: #Q OperationCanceledException?
        }

        var product = await itemService.GetByIdAsync(request.ProductId);

        if (product == null)
        {
            throw new InvalidOperationException($"Product with id {request.ProductId} not found");//TODO: #Q OperationCanceledException?
        }

        if (!product.TrackInventory.GetValueOrDefault())//TODO: #Q what means TrackInventory==null ?
        {
            result.Results.Add(await CreateDefaultPickupLocation(request.CultureName));
        }
        else
        {
            var productInventorySearchCriteria = AbstractTypeFactory<ProductInventorySearchCriteria>.TryCreateInstance();
            productInventorySearchCriteria.ProductId = request.ProductId;
            //productInventorySearchCriteria.Take = request.Take;//TODO: #Q do we need paging here?
            //productInventorySearchCriteria.Skip = request.Skip;

            var productInventories = await productInventorySearchService.SearchProductInventoriesAsync(productInventorySearchCriteria);

            var mainFulfillmentCenter = await GetMainFulfillmentCenter(store, product, productInventories.Results, request.CultureName);
            if (mainFulfillmentCenter != null)
            {
                result.Results.Add(mainFulfillmentCenter);
            }

            var transferFulfillmentCenters = await GetTransferFulfillmentCenters(store, product, productInventories.Results, request.CultureName);
            if (transferFulfillmentCenters != null)
            {
                result.Results.AddRange(transferFulfillmentCenters);
            }
        }

        if (store.Settings.GetValue<bool>(ModuleConstants.Settings.GlobalTransferEnabled))
        {
            result.Results.Add(await CreateGlobalTransferPickupLocation(request.CultureName));
        }

        return result;
    }

    protected virtual async Task<ProductPickupLocation> GetMainFulfillmentCenter(Store store, CatalogProduct product, IList<InventoryInfo> productInventories, string cultureName)
    {
        if (!store.MainFulfillmentCenterId.IsNullOrEmpty())
        {
            var availableMainFulfillmentCenterIventory = productInventories
                .Where(x => x.FulfillmentCenterId == store.MainFulfillmentCenterId)
                .Where(x => x.InStockQuantity > 0)
                .FirstOrDefault();

            if (availableMainFulfillmentCenterIventory != null)
            {
                return await CreatePickupLocationFromProductInventory(availableMainFulfillmentCenterIventory, ProductPickupAvailability.Today, cultureName);
            }
        }

        return null;
    }

    protected virtual async Task<IList<ProductPickupLocation>> GetTransferFulfillmentCenters(Store store, CatalogProduct product, IList<InventoryInfo> productInventories, string cultureName)
    {
        var result = new List<ProductPickupLocation>();

        var availableTransferFulfillmentCenterInventories = productInventories
            .Where(x => store.AdditionalFulfillmentCenterIds.Contains(x.FulfillmentCenterId))
            .Where(x => x.InStockQuantity > 0);

        foreach (var availableTransferFulfillmentCenterInventory in availableTransferFulfillmentCenterInventories)
        {
            result.Add(await CreatePickupLocationFromProductInventory(availableTransferFulfillmentCenterInventory, ProductPickupAvailability.Transfer, cultureName));
        }

        return result;
    }

    protected virtual async Task<ProductPickupLocation> CreateDefaultPickupLocation(string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.AvailabilityType = ProductPickupAvailability.Today;
        result.Note = await GetProductPickupLocationNote(ProductPickupAvailability.Today, cultureName);

        return result;
    }

    protected virtual async Task<ProductPickupLocation> CreatePickupLocationFromProductInventory(InventoryInfo productInventoryInfo, string productPickupAvailability, string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.AvailabilityType = productPickupAvailability;
        result.Note = await GetProductPickupLocationNote(productPickupAvailability, cultureName);
        result.Name = productInventoryInfo.FulfillmentCenter.Name;
        result.Address = productInventoryInfo.FulfillmentCenter.Address.ToString();
        result.AvailableQuantity = productInventoryInfo.InStockQuantity;

        return result;
    }

    protected virtual async Task<ProductPickupLocation> CreateGlobalTransferPickupLocation(string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.AvailabilityType = ProductPickupAvailability.GlobalTransfer;
        result.Note = await GetProductPickupLocationNote(ProductPickupAvailability.GlobalTransfer, cultureName);

        return result;
    }

    protected virtual async Task<string> GetProductPickupLocationNote(string productPickupAvailability, string cultureName)
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
}
