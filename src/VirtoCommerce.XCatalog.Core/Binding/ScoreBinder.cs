using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Binding;

namespace VirtoCommerce.XCatalog.Core.Binding;

public class ScoreBinder : IIndexModelBinder
{
    public BindingInfo BindingInfo { get; set; }

    public object BindModel(SearchDocument searchDocument)
    {
        var fieldName = BindingInfo?.FieldName;
        return !string.IsNullOrEmpty(fieldName) && searchDocument.TryGetValue(fieldName, out var value) && value is double
            ? value
            : null;
    }
}
