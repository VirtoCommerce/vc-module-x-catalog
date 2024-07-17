using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries
{
    public class SearchVideoQuery : IQuery<SearchVideoQueryResponse>
    {
        public int Skip { get; set; }
        public int Take { get; set; }
        public string CultureName { get; set; }
        public string OwnerId { get; set; }
        public string OwnerType { get; set; }
    }
}
