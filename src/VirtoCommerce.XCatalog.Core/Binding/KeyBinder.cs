using VirtoCommerce.Xapi.Core.Binding;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.XCatalog.Core.Binding
{
    public class KeyBinder : IIndexModelBinder
    {
        public BindingInfo BindingInfo { get; set; }

        public virtual object BindModel(SearchDocument searchDocument)
        {
            return searchDocument.Id;
        }
    }
}
