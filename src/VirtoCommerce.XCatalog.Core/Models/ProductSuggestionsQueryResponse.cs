using System.Collections.Generic;

namespace VirtoCommerce.XCatalog.Core.Models;

public class ProductSuggestionsQueryResponse
{
    public IList<string> Suggestions { get; set; } = new List<string>();
}
