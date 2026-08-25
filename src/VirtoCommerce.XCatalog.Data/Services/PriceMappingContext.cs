using System.Collections.Generic;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models;

namespace VirtoCommerce.XCatalog.Data.Services;

/// <summary>
/// Ambient context for <see cref="IXCatalogMapper.ToProductPromoEntry"/> and
/// <see cref="IXCatalogMapper.ToProductPrices"/>; not every member is needed by every call.
/// </summary>
public class PriceMappingContext : MappingContext
{
    public Currency Currency { get; set; }

    public IEnumerable<Currency> AllStoreCurrencies { get; set; }

    public IEnumerable<Pricelist> Pricelists { get; set; }
}
