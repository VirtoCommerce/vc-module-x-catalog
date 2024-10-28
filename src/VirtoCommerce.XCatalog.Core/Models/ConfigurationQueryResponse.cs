using System.Collections.Generic;

namespace VirtoCommerce.XCatalog.Core.Models;
public class ConfigurationQueryResponse
{
    public IList<ExpConfigurationSection> ConfigurationSections { get; set; } = [];
}

