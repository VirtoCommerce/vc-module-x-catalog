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
                      object[] objs => objs.Select(x => (string)x).ToList(),
                      string str => [str],
                      _ => Enumerable.Empty<string>().ToList()
                  }
                  : Enumerable.Empty<string>().ToList();

            return result;
        }
    }
}
