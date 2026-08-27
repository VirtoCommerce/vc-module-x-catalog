using System.Collections.Generic;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Tests.Helpers;

/// <summary>
/// Builds the <see cref="SearchProductResponse"/> carrier - the fixture any test needs once it touches a
/// <c>MappingContext</c> derivative that carries this response whole (<see cref="ProductPricesMappingContext"/>,
/// <see cref="PromoPriceMappingContext"/>, <c>CatalogFacetMappingContext</c>), so the shape used across those
/// tests cannot drift from the production shape (<c>EvalProductsPricesMiddleware</c>,
/// <c>EvalProductsDiscountsMiddleware</c>, <c>SearchProductQueryHandler</c>).
/// </summary>
public static class SearchProductResponseBuilder
{
    public static SearchProductResponse Build(Currency currency = null, IEnumerable<Currency> allStoreCurrencies = null)
    {
        return new SearchProductResponse
        {
            Query = new SearchProductQuery(),
            Currency = currency,
            AllStoreCurrencies = allStoreCurrencies,
        };
    }

    public static PromoPriceMappingContext ToPromoPriceMappingContext(this SearchProductResponse response)
    {
        return new PromoPriceMappingContext { Response = response };
    }

    public static ProductPricesMappingContext ToProductPricesMappingContext(this SearchProductResponse response, IEnumerable<Pricelist> pricelists = null)
    {
        return new ProductPricesMappingContext { Response = response, Pricelists = pricelists };
    }
}
