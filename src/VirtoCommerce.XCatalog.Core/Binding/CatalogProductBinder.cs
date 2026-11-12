using System;
using Newtonsoft.Json.Linq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Binding;

namespace VirtoCommerce.XCatalog.Core.Binding;

public class CatalogProductBinder : IIndexModelBinder
{
    private static readonly Type _productType = AbstractTypeFactory<CatalogProduct>.TryCreateInstance().GetType();

    public BindingInfo BindingInfo { get; set; } = new BindingInfo { FieldName = "__object" };

    public virtual object BindModel(SearchDocument searchDocument)
    {
        if (!searchDocument.TryGetValue(BindingInfo.FieldName, out var obj))
        {
            // No object in index
            return null;
        }

        var result = Deserialize(obj);

        if (result == null)
        {
            return null;
        }

        result.BindIndexedProperties(searchDocument);

        return result;
    }

    private static CatalogProduct Deserialize(object obj)
    {
        switch (obj)
        {
            case string sObj:
                return (CatalogProduct)IndexedObjectReader.Read(sObj, _productType);

            case JObject jobj:
                return (CatalogProduct)jobj.ToObject(_productType);

            default:
                return null;
        }
    }
}
