using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.CatalogModule.Core.Serialization;
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
                        var indexedPrices = new List<IndexedPrice>();
                        foreach (var sObj in jArray.OfType<string>())
                        {
                            try
                            {
                                var indexedPrice = ProductJsonSerializer.Deserialize<IndexedPrice>(sObj);

                                if (indexedPrice != null)
                                {
                                    indexedPrices.Add(indexedPrice);
                                }
                            }
                            // JObject.Parse rejected a payload that was not an object; the direct path
                            // reports that as a serialization error, and both must skip only this price.
                            catch (JsonException)
                            {
                                // Intentionally left empty
                            }
                        }

                        if (indexedPrices.Count == 0)
                        {
                            indexedPrices = jArray.OfType<JObject>().Select(x => x.ToObject<IndexedPrice>()).ToList();
                        }

                        foreach (var indexedPrice in indexedPrices)
                        {
                            AddPrice(result, indexedPrice);
                        }

                        break;
                    }

                case JObject jObject:
                    {
                        AddPrice(result, jObject.ToObject<IndexedPrice>());
                        break;
                    }
            }

            return result;
        }

        private static void AddPrice(List<Price> result, IndexedPrice indexedPrice)
        {
            result.Add(new Price
            {
                Currency = indexedPrice.Currency,
                List = indexedPrice.Value,
            });
        }
    }
}
