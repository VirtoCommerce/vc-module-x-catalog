using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Binding;

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
