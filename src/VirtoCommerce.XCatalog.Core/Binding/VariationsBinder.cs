using System.Linq;
using VirtoCommerce.Xapi.Core.Binding;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.XCatalog.Core.Binding
{
    public class VariationsBinder : IIndexModelBinder
    {
        public BindingInfo BindingInfo { get; set; } = new BindingInfo { FieldName = "__variations" };

        public virtual object BindModel(SearchDocument searchDocument)
        {
            var fieldName = BindingInfo.FieldName;

            return searchDocument.ContainsKey(fieldName) && searchDocument[fieldName] is object[] objs
                ? objs.Select(x => (string)x).ToList()
                : Enumerable.Empty<string>().ToList();
        }
    }
}
