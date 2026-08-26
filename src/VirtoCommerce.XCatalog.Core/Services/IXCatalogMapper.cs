using System.Collections.Generic;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using Aggregation = VirtoCommerce.CatalogModule.Core.Model.Search.Aggregation;
using ProductPrice = VirtoCommerce.Xapi.Core.Models.ProductPrice;

namespace VirtoCommerce.XCatalog.Core.Services;

public interface IXCatalogMapper
{
    FacetResult ToFacetResult(Aggregation source, FacetMappingContext context);

    void MapTo(IList<IFilter> filters, PropertySearchCriteria criteria);

    ExpCategory ToExpCategory(SearchDocument source);

    ExpProduct ToExpProduct(SearchDocument source);

    SearchProductQuery ToSearchProductQuery(LoadProductsQuery source);

    SearchCategoryQuery ToSearchCategoryQuery(LoadCategoryQuery source);

    ProductAssociationSearchCriteria ToProductAssociationSearchCriteria(SearchProductAssociationsQuery source);

    PropertyDictionaryItemSearchCriteria ToPropertyDictionaryItemSearchCriteria(SearchPropertyDictionaryItemQuery source);

    /// <exception cref="System.ArgumentNullException"><paramref name="context"/> or its <see cref="PromoPriceMappingContext.Response"/> or that response's <c>Currency</c> is null.</exception>
    ProductPromoEntry ToProductPromoEntry(ExpProduct source, PromoPriceMappingContext context);

    /// <returns>An empty collection if <paramref name="source"/> is null.</returns>
    IEnumerable<TaxLine> ToTaxLines(ExpProduct source);

    /// <returns>An empty collection if <paramref name="source"/> is null.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="context"/> or its <see cref="ProductPricesMappingContext.Response"/> or that response's <c>AllStoreCurrencies</c> is null.</exception>
    IEnumerable<ProductPrice> ToProductPrices(IEnumerable<Price> source, ProductPricesMappingContext context);

    ExpVendor ToExpVendor(Member source);
}
