using System.Collections.Generic;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Models;

/// <summary>
/// Ambient context for <see cref="Services.IXCatalogMapper.ToProductPrices"/>.
/// </summary>
public class ProductPricesMappingContext : MappingContext
{
    public SearchProductResponse Response { get; set; }

    public IEnumerable<Pricelist> Pricelists { get; set; }
}
