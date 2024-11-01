using System.Collections.Generic;

namespace VirtoCommerce.XCatalog.Core.Models;

public class ExpConfigurationSection
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public bool IsRequired { get; set; }
    public int Quantity { get; set; }

    public IList<string> ProductIds { get; set; } = [];
    public IList<ExpProduct> Products { get; set; } = [];
}

