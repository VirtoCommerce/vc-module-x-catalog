using System.Collections.Generic;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries
{
    public class LoadPromotionsQuery : IQuery<LoadPromotionsResponse>
    {
        public IEnumerable<string> Ids { get; set; }
    }
}
