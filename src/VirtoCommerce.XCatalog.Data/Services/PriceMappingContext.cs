using System.Collections.Generic;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models;

namespace VirtoCommerce.XCatalog.Data.Services;

/// <summary>
/// Ambient context for the pricing-related mapping methods on <see cref="IXCatalogMapper"/>:
/// <see cref="IXCatalogMapper.ToProductPromoEntry"/> and <see cref="IXCatalogMapper.ToProductPrices"/>.
/// Not every member is populated for every call: <c>ToProductPromoEntry</c> only reads
/// <see cref="Currency"/>; <c>ToProductPrices</c> only reads <see cref="AllStoreCurrencies"/> and
/// <see cref="Pricelists"/>.
/// </summary>
public class PriceMappingContext : MappingContext
{
    public Currency Currency { get; set; }

    public IEnumerable<Currency> AllStoreCurrencies { get; set; }

    public IEnumerable<Pricelist> Pricelists { get; set; }
}
