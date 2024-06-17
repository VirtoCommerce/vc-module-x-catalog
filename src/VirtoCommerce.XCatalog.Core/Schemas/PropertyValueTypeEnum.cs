using GraphQL.Types;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class PropertyValueTypeEnum : EnumerationGraphType<PropertyValueType>
    {
        public PropertyValueTypeEnum()
        {
            Name = "PropertyValueTypes";
            Description = "The type of catalog property value.";
        }
    }
}
