using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.CatalogModule.Core.Model;
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
            var result = default(CatalogProduct);

            if (!searchDocument.TryGetValue(BindingInfo.FieldName, out var obj))
            {
                // No object in index
                return result;
            }

            // check if __object document field name contains string or jObject
            if (obj is string sObj)
            {
                try
                {
                    obj = JObject.Parse(sObj);
                }
                catch (JsonReaderException)
                {
                    return result;
                }
            }

            if (obj is JObject jobj)
            {
                result = (CatalogProduct)jobj.ToObject(_productType);

                var productProperties = result.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var property in productProperties)
                {
                    var binder = property.GetIndexModelBinder();

                    if (binder != null)
                    {
                        property.SetValue(result, binder.BindModel(searchDocument));
                    }
                }
            }

            return result;
        }
    }
}
