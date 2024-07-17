using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Binding;

namespace VirtoCommerce.XCatalog.Core.Binding
{
    public class MinVariationPriceBinder : IIndexModelBinder
    {
        public BindingInfo BindingInfo { get; set; } = new BindingInfo { FieldName = "__minvariationprice" };

        public object BindModel(SearchDocument searchDocument)
        {
            var result = new List<Price>();

            if (!searchDocument.TryGetValue(BindingInfo.FieldName, out var pricesDocumentRecord))
            {
                return result;
            }

            switch (pricesDocumentRecord)
            {
                case Array jArray:
                    {
                        var jObjects = new List<JObject>();
                        foreach (var sObj in jArray.OfType<string>())
                        {
                            try
                            {
                                var jObj = JObject.Parse(sObj);
                                jObjects.Add(jObj);
                            }
                            catch (JsonReaderException)
                            {
                                // Intentionally left empty
                            }
                        }

                        jObjects = jObjects.Count != 0 ? jObjects : jArray.OfType<JObject>().ToList();
                        foreach (var jObject in jObjects)
                        {
                            AddPrice(result, jObject);
                        }

                        break;
                    }

                case JObject jObject:
                    {
                        AddPrice(result, jObject);
                        break;
                    }
            }

            return result;
        }

        private static void AddPrice(List<Price> result, JObject jObject)
        {
            var indexedPrice = jObject.ToObject<IndexedPrice>();
            result.Add(new Price
            {
                Currency = indexedPrice.Currency,
                List = indexedPrice.Value,
            });
        }
    }
}
