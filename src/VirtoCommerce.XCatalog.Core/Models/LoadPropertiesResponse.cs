using System.Collections.Generic;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.XCatalog.Core.Models
{
    public class LoadPropertiesResponse
    {
        public IDictionary<string, Property> Properties { get; set; }
    }
}
