using System.Collections.Generic;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;

namespace VirtoCommerce.XCatalog.Core.Models
{
    public class LoadPromotionsResponse
    {
        public IDictionary<string, Promotion> Promotions { get; set; }
    }
}
