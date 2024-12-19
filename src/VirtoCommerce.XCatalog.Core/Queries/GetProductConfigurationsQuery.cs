using System.Collections.Generic;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.XCatalog.Core.Queries;
public class GetProductConfigurationsQuery : IQuery<Dictionary<string, bool>>
{
    public string[] ProductIds { get; set; } = [];
}
