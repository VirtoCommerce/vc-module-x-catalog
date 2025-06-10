using System.Collections.Generic;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Queries
{
    public class LoadRelatedCatalogOutlineQuery : CatalogQueryBase<LoadRelatedCatalogOutlineResponse>
    {
        public IList<Outline> Outlines { get; set; }
    }
}
