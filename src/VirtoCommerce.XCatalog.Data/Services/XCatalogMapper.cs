using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.Xapi.Core.Binding;
using VirtoCommerce.Xapi.Core.Index;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.Xapi.Core.Services;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Services;
using Aggregation = VirtoCommerce.CatalogModule.Core.Model.Search.Aggregation;
using AggregationItem = VirtoCommerce.CatalogModule.Core.Model.Search.AggregationItem;
using AggregationLabel = VirtoCommerce.CatalogModule.Core.Model.Search.AggregationLabel;
using AggregationStatistics = VirtoCommerce.CatalogModule.Core.Model.Search.AggregationStatistics;
using CatalogModuleConstants = VirtoCommerce.CatalogModule.Core.ModuleConstants;
using ProductPrice = VirtoCommerce.Xapi.Core.Models.ProductPrice;

namespace VirtoCommerce.XCatalog.Data.Services;

public class XCatalogMapper : IXCatalogMapper
{
    private readonly IFacetMapper _facetMapper;

    public XCatalogMapper(IFacetMapper facetMapper)
    {
        _facetMapper = facetMapper;
    }

    public virtual FacetResult ToFacetResult(Aggregation source, FacetMappingContext context)
    {
        return _facetMapper.ToFacetResult(ToAggregationFacetSource(source), context);
    }

    protected virtual AggregationFacetSource ToAggregationFacetSource(Aggregation source)
    {
        if (source == null)
        {
            return null;
        }

        return new AggregationFacetSource
        {
            AggregationType = source.AggregationType,
            Field = source.Field,
            // Defaulting to ascending is x-catalog's own historical behavior; the shared FacetMapper
            // itself leaves an unset TermValuesSortingType unsorted.
            TermValuesSortingType = source.TermValuesSortingType.IsNullOrEmpty()
                ? CatalogModuleConstants.TermValuesSortingTypeNameAscending
                : source.TermValuesSortingType,
            Labels = source.Labels?.Select(ToAggregationFacetLabel).ToList(),
            Items = source.Items?.Select(ToAggregationFacetItem).ToList(),
            Statistics = ToAggregationFacetStatistics(source.Statistics),
        };
    }

    protected virtual AggregationFacetItem ToAggregationFacetItem(AggregationItem source)
    {
        return new AggregationFacetItem
        {
            Value = source.Value,
            Count = source.Count,
            IsApplied = source.IsApplied,
            Labels = source.Labels?.Select(ToAggregationFacetLabel).ToList(),
            RequestedLowerBound = source.RequestedLowerBound,
            RequestedUpperBound = source.RequestedUpperBound,
            IncludeLower = source.IncludeLower,
            IncludeUpper = source.IncludeUpper,
        };
    }

    protected virtual AggregationFacetStatistics ToAggregationFacetStatistics(AggregationStatistics source)
    {
        if (source == null)
        {
            return null;
        }

        return new AggregationFacetStatistics
        {
            Min = source.Min,
            Max = source.Max,
        };
    }

    protected virtual AggregationFacetLabel ToAggregationFacetLabel(AggregationLabel source)
    {
        return new AggregationFacetLabel
        {
            Language = source.Language,
            Label = source.Label,
        };
    }

    public virtual void MapTo(IList<IFilter> filters, PropertySearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (filters == null)
        {
            return;
        }

        foreach (var term in filters.OfType<TermFilter>())
        {
            term.MapTo(criteria);
        }
    }

    public virtual ExpCategory ToExpCategory(SearchDocument source)
    {
        if (source == null)
        {
            return null;
        }

        return new GenericModelBinder<ExpCategory>().BindModel(source) as ExpCategory;
    }

    public virtual ExpProduct ToExpProduct(SearchDocument source)
    {
        if (source == null)
        {
            return null;
        }

        return new GenericModelBinder<ExpProduct>().BindModel(source) as ExpProduct;
    }

    public virtual SearchProductQuery ToSearchProductQuery(LoadProductsQuery source)
    {
        if (source == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<SearchProductQuery>.TryCreateInstance();

        result.StoreId = source.StoreId;
        result.UserId = source.UserId;
        result.CultureName = source.CultureName;
        result.CurrencyCode = source.CurrencyCode;
        result.PreviousOutline = source.PreviousOutline;
        result.OrganizationId = source.OrganizationId;
        result.Store = source.Store;
        result.IncludeFields = source.IncludeFields?.ToList() ?? [];
        result.ObjectIds = source.ObjectIds?.ToArray();
        result.EvaluatePromotions = source.EvaluatePromotions;

        return result;
    }

    public virtual SearchCategoryQuery ToSearchCategoryQuery(LoadCategoryQuery source)
    {
        if (source == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<SearchCategoryQuery>.TryCreateInstance();

        result.StoreId = source.StoreId;
        result.UserId = source.UserId;
        result.CultureName = source.CultureName;
        result.CurrencyCode = source.CurrencyCode;
        result.PreviousOutline = source.PreviousOutline;
        result.OrganizationId = source.OrganizationId;
        result.Store = source.Store;
        result.IncludeFields = source.IncludeFields?.ToList() ?? [];
        result.ObjectIds = source.ObjectIds;

        return result;
    }

    public virtual ProductAssociationSearchCriteria ToProductAssociationSearchCriteria(SearchProductAssociationsQuery source)
    {
        if (source == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<ProductAssociationSearchCriteria>.TryCreateInstance();

        result.ObjectIds = source.ObjectIds;
        result.Keyword = source.Keyword;
        result.Group = source.Group;
        result.Sort = source.Sort;
        result.Skip = source.Skip;
        result.Take = source.Take;

        return result;
    }

    public virtual PropertyDictionaryItemSearchCriteria ToPropertyDictionaryItemSearchCriteria(SearchPropertyDictionaryItemQuery source)
    {
        if (source == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<PropertyDictionaryItemSearchCriteria>.TryCreateInstance();

        result.Skip = source.Skip;
        result.Take = source.Take;
        result.PropertyIds = source.PropertyIds;

        return result;
    }

    public virtual ProductPromoEntry ToProductPromoEntry(ExpProduct source, PromoPriceMappingContext context)
    {
        if (source == null)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Response);
        ArgumentNullException.ThrowIfNull(context.Response.Currency);

        var result = AbstractTypeFactory<ProductPromoEntry>.TryCreateInstance();

        var productPrice = source.AllPrices.FirstOrDefault(x => x.Currency.Code.EqualsIgnoreCase(context.Response.Currency.Code));

        result.CatalogId = source.IndexedProduct.CatalogId;
        result.CategoryId = source.IndexedProduct.CategoryId;
        result.ProductId = source.Id;
        result.ParentId = source.IndexedProduct.MainProductId;
        result.Code = source.IndexedProduct.Code;
        result.Outline = source.IndexedProduct.Outline;

        if (productPrice != null)
        {
            result.Discount = productPrice.DiscountAmount.Amount;
            result.Price = productPrice.SalePrice.Amount;
            result.ListPrice = productPrice.ListPrice.Amount;
        }

        result.InStockQuantity = (int)source.AvailableQuantity;
        result.Quantity = 1;

        return result;
    }

    public virtual IEnumerable<TaxLine> ToTaxLines(ExpProduct source)
    {
        if (source == null)
        {
            return [];
        }

        var result = new List<TaxLine>();

        foreach (var price in source.AllPrices)
        {
            var taxLine = AbstractTypeFactory<TaxLine>.TryCreateInstance();
            taxLine.Id = source.Id;
            taxLine.Code = source.IndexedProduct.Code;
            taxLine.Name = source.IndexedProduct.Name;
            taxLine.TaxType = source.IndexedProduct.TaxType;
            // Special case when a product has a 100% discount and the tax still needs to be calculated on the old value.
            taxLine.Amount = price.ActualPrice.Amount > 0 ? price.ActualPrice.Amount : price.SalePrice.Amount;
            result.Add(taxLine);

            // A tax line is also needed for each tier price.
            foreach (var tierPrice in price.TierPrices)
            {
                var tierTaxLine = AbstractTypeFactory<TaxLine>.TryCreateInstance();
                tierTaxLine.Id = source.Id;
                tierTaxLine.Code = source.IndexedProduct.Code;
                tierTaxLine.Name = source.IndexedProduct.Name;
                tierTaxLine.TaxType = source.IndexedProduct.TaxType;
                tierTaxLine.Quantity = (int)tierPrice.Quantity;
                tierTaxLine.Amount = tierPrice.Price.Amount;
                result.Add(tierTaxLine);
            }
        }

        return result.ToArray();
    }

    public virtual IEnumerable<ProductPrice> ToProductPrices(IEnumerable<Price> source, ProductPricesMappingContext context)
    {
        if (source == null)
        {
            return [];
        }

        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Response);
        ArgumentNullException.ThrowIfNull(context.Response.AllStoreCurrencies);

        var result = new List<ProductPrice>();

        // Group prices by currency.
        var groupedByCurrency = ToProductPricesByCurrency(source, context).GroupBy(x => x.Currency).Where(x => x.Any());
        foreach (var currencyGroup in groupedByCurrency)
        {
            // For each currency, need the nominal price (with the minimum quantity).
            var orderedPrices = currencyGroup.OrderBy(x => x.MinQuantity ?? 0).ThenBy(x => x.ListPrice).ToList();
            var nominalPrice = orderedPrices.First();

            // Add the other prices to the nominal price as tier prices.
            nominalPrice.TierPrices.AddRange(orderedPrices.Select(x => new TierPrice(nominalPrice.ListPrice, x.SalePrice, x.MinQuantity ?? 1)));

            result.Add(nominalPrice);
        }

        return result;
    }

    protected virtual IEnumerable<ProductPrice> ToProductPricesByCurrency(IEnumerable<Price> prices, ProductPricesMappingContext context)
    {
        var allCurrencies = context.Response.AllStoreCurrencies.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase).WithDefaultValue(null);
        var pricelists = context.Pricelists?.ToList() ?? [];

        foreach (var price in prices)
        {
            var currency = allCurrencies[price.Currency];
            if (currency != null)
            {
                var productPrice = AbstractTypeFactory<ProductPrice>.TryCreateInstance(nameof(ProductPrice), currency);
                productPrice.ProductId = price.ProductId;
                productPrice.PricelistId = price.PricelistId;
                productPrice.StartDate = price.StartDate;
                productPrice.EndDate = price.EndDate;
                productPrice.ListPrice = new Money(price.List, currency);
                productPrice.SalePrice = price.Sale == null ? productPrice.ListPrice : new Money(price.Sale ?? 0m, currency);
                productPrice.MinQuantity = price.MinQuantity;

                var pricelist = pricelists.FirstOrDefault(x => x.Id == price.PricelistId);
                productPrice.PricelistName = pricelist?.Name;

                yield return productPrice;
            }
        }
    }

    public virtual ExpVendor ToExpVendor(Member source)
    {
        if (source == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<ExpVendor>.TryCreateInstance();

        result.Id = source.Id;
        result.Name = source.Name;
        result.Type = source.MemberType;

        return result;
    }
}
