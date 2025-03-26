using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Binding;

namespace VirtoCommerce.XCatalog.Core.Binding
{
    public class VariationsBinder : IIndexModelBinder
    {
        public BindingInfo BindingInfo { get; set; } = new BindingInfo { FieldName = "__variations" };

        public virtual object BindModel(SearchDocument searchDocument)
        {
            var fieldName = BindingInfo.FieldName;

            var result = searchDocument.TryGetValue(fieldName, out var value)
                  ? value switch
                  {
                      IList<object> list => list.OfType<string>().ToList(),
                      string str => [str],
                      _ => []
                  }
                  : [];

            return result;
        }
    }
}
