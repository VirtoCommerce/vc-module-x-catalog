using VirtoCommerce.Xapi.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Models;

/// <summary>
/// Ambient context for <see cref="Services.IXCatalogMapper.ToProductPromoEntry"/>.
/// </summary>
public class PromoPriceMappingContext : MappingContext
{
    public SearchProductResponse Response { get; set; }
}
