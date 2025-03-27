using System.Collections.Generic;

namespace VirtoCommerce.XCatalog.Core.Models;

public class Breadcrumbs
{
    public string Path { get; set; }
    public IList<Breadcrumb> Items { get; set; }
}
