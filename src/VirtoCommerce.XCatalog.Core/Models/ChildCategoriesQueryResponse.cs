using System.Collections.Generic;

namespace VirtoCommerce.XCatalog.Core.Models;

public class ChildCategoriesQueryResponse
{
    public IList<ExpCategory> ChildCategories { get; set; }
}
