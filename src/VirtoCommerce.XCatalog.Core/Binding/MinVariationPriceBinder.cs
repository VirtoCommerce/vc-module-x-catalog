using System;
using System.Collections.Generic;
using System.Linq;
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
                        var indexedPrices = new List<IndexedPrice>();
                        foreach (var sObj in jArray.OfType<string>())
                        {
                            var indexedPrice = IndexedObjectReader.Read<IndexedPrice>(sObj);

                            if (indexedPrice != null)
                            {
                                indexedPrices.Add(indexedPrice);
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
