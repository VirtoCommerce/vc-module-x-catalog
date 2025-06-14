using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Seo.Core.Models;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Binding;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.XCatalog.Core.Binding;
using VirtoCommerce.XCatalog.Core.Specifications;
using ProductPrice = VirtoCommerce.Xapi.Core.Models.ProductPrice;

namespace VirtoCommerce.XCatalog.Core.Models
{
    public class ExpProduct : IHasRelevanceScore
    {
        public string Id => IndexedProduct?.Id;

        [BindIndexField(FieldName = "__object", BinderType = typeof(CatalogProductBinder))]
        public virtual CatalogProduct IndexedProduct { get; set; }

        [BindIndexField(FieldName = "__variations", BinderType = typeof(VariationsBinder))]
        public virtual IList<string> IndexedVariationIds { get; set; } = new List<string>();

        [BindIndexField(FieldName = "__minvariationprice", BinderType = typeof(MinVariationPriceBinder))]
        public IList<Price> IndexedMinVariationPrices { get; set; } = new List<Price>();

        [BindIndexField(FieldName = SearchModule.Core.ModuleConstants.RelevanceScore, BinderType = typeof(DefaultPropertyIndexBinder))]
        public double? RelevanceScore { get; set; }

        [BindIndexField(BinderType = typeof(KeyBinder))]
        public virtual string Key { get; set; }

        public SeoInfo SeoInfo { get; set; }

        public bool IsBuyable
        {
            get
            {
                return AbstractTypeFactory<CatalogProductIsBuyableSpecification>.TryCreateInstance().IsSatisfiedBy(this);
            }
        }

        public bool IsAvailable
        {
            get
            {
                return AbstractTypeFactory<CatalogProductIsAvailableSpecification>.TryCreateInstance().IsSatisfiedBy(this);
            }
        }

        public bool IsInStock
        {
            get
            {
                return AbstractTypeFactory<CatalogProductIsInStockSpecification>.TryCreateInstance().IsSatisfiedBy(this);
            }
        }

        public ProductPrice MinVariationPrice { get; set; }

        public IList<ProductPrice> AllPrices { get; set; } = new List<ProductPrice>();

        /// <summary>
        /// Inventory of all fulfillment centers.
        /// </summary>
        public IList<InventoryInfo> AllInventories { get; set; } = new List<InventoryInfo>();

        /// <summary>
        /// Inventory for default fulfillment center
        /// </summary>
        public InventoryInfo Inventory { get; private set; }

        public EditorialReview Description { get; set; }

        public ExpVendor Vendor { get; set; }

        /// <summary>
        /// Product rating
        /// </summary>
        public ExpRating Rating { get; set; }

        public bool InWishlist { get; set; }

        public IList<string> WishlistIds { get; set; } = [];

        public bool IsPurchased { get; set; }

        public virtual long AvailableQuantity
        {
            get
            {
                long result = 0;

                if (IndexedProduct.TrackInventory.GetValueOrDefault(true) && AllInventories != null)
                {
                    foreach (var inventory in AllInventories)
                    {
                        result += Math.Max(0, inventory.InStockQuantity - inventory.ReservedQuantity);
                    }
                }
                return result;
            }
        }

        public virtual void ApplyStaticDiscounts()
        {
            foreach (var productPrice in AllPrices)
            {
                productPrice.DiscountAmount = new Money(Math.Max(0, (productPrice.ListPrice - productPrice.SalePrice).Amount), productPrice.Currency);
            }
        }

        public virtual void ApplyRewards(CatalogItemAmountReward[] allRewards)
        {
            var productRewards = allRewards?.Where(r => r.ProductId.IsNullOrEmpty() || r.ProductId.EqualsIgnoreCase(Id));
            if (productRewards == null)
            {
                return;
            }

            var rewardsMap = AllPrices
                   .Select(x => x.Currency)
                   .Distinct()
                   .ToDictionary(x => x, _ => productRewards);

            foreach (var productPrice in AllPrices)
            {
                var mappedRewards = rewardsMap[productPrice.Currency];
                productPrice.Discounts.Clear();
                productPrice.DiscountAmount = new Money(Math.Max(0, (productPrice.ListPrice - productPrice.SalePrice).Amount), productPrice.Currency);

                foreach (var reward in mappedRewards)
                {
                    if (!reward.IsValid)
                    {
                        continue;
                    }

                    var priceAmount = (productPrice.ListPrice - productPrice.DiscountAmount).Amount;

                    var discount = new Discount
                    {
                        DiscountAmount = reward.GetTotalAmount(priceAmount, 1, productPrice.Currency),
                        Description = reward.Promotion.Description,
                        Coupon = reward.Coupon,
                        PromotionId = reward.Promotion.Id
                    };

                    productPrice.Discounts.Add(discount);

                    if (discount.DiscountAmount > 0)
                    {
                        productPrice.DiscountAmount += discount.DiscountAmount;

                        foreach (var tierPrice in productPrice.TierPrices)
                        {
                            tierPrice.DiscountAmount += reward.GetTotalAmount(tierPrice.ActualPrice.Amount, 1, productPrice.Currency);
                        }
                    }
                }
            }
        }

        public virtual void ApplyStoreInventories(IEnumerable<InventoryInfo> inventories, Store store)
        {
            ArgumentNullException.ThrowIfNull(inventories);
            ArgumentNullException.ThrowIfNull(store);

            var availFulfilmentCentersIds = (store.AdditionalFulfillmentCenterIds ?? Array.Empty<string>()).Concat([store.MainFulfillmentCenterId]);

            AllInventories.Clear();
            Inventory = null;
            AllInventories = inventories.Where(x => x.ProductId == Id && availFulfilmentCentersIds.Contains(x.FulfillmentCenterId)).ToList();

            Inventory = AllInventories.OrderByDescending(x => Math.Max(0, x.InStockQuantity - x.ReservedQuantity)).FirstOrDefault();

            if (store.MainFulfillmentCenterId != null)
            {
                var mainFfc = AllInventories.FirstOrDefault(x => x.FulfillmentCenterId == store.MainFulfillmentCenterId);
                Inventory = mainFfc != null && mainFfc.InStockQuantity - mainFfc.ReservedQuantity > 0 ? mainFfc : Inventory;
            }
        }
    }
}
