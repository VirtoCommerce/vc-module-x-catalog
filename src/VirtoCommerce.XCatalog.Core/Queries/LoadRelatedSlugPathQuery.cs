using System.Collections.Generic;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries
{
    public class LoadRelatedSlugPathQuery : CatalogQueryBase<LoadRelatedSlugPathResponse>
    {
        public IList<Outline> Outlines { get; set; }
    }
}
