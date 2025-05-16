using System.Collections.Generic;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.XCatalog.Core.Models;

public class BrandAggregate : Entity, ISeoSupport
{
    public string Name { get; set; }
    public string BrandPropertyName { get; set; }

    public Store Store { get; set; }
    public Catalog Catalog { get; set; }

    public LocalizedString LocalizedName { get; set; }
    public IList<Property> Properties { get; set; } = [];
    public IList<CategoryDescription> Descriptions { get; set; } = [];
    public IList<Image> Images = [];

    public IList<SeoInfo> SeoInfos { get; set; } = [];
    public string SeoObjectType => ModuleConstants.BrandSeoType;
}
