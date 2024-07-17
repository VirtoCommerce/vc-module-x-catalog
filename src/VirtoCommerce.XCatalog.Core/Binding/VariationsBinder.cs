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

            return searchDocument.TryGetValue(fieldName, out var value) && value is object[] objs
                ? objs.Select(x => (string)x).ToList()
                : Enumerable.Empty<string>().ToList();
        }
    }
}
