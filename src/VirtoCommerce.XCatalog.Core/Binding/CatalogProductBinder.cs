using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Serialization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Binding;

namespace VirtoCommerce.XCatalog.Core.Binding
{
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

            var productProperties = result.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in productProperties)
            {
                var binder = property.GetIndexModelBinder();

                if (binder != null)
                {
                    property.SetValue(result, binder.BindModel(searchDocument));
                }
            }

            return result;
        }

        private static CatalogProduct Deserialize(object obj)
        {
            switch (obj)
            {
                case string sObj:
                    try
                    {
                        return (CatalogProduct)ProductJsonSerializer.Deserialize(sObj, _productType);
                    }
                    // JObject.Parse rejected a payload that was not an object; the direct path reports
                    // that as a serialization error, so both must stay non-fatal for the whole page.
                    catch (JsonException)
                    {
                        return null;
                    }

                case JObject jobj:
                    return (CatalogProduct)jobj.ToObject(_productType);

                default:
                    return null;
            }
        }
    }
}
