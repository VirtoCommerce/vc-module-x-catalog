using GraphQL.Language.AST;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Schemas.ScalarTypes;

namespace VirtoCommerce.XCatalog.Core.Schemas.ScalarTypes
{
    public class PropertyValueGraphType : AnyValueGraphType
    {
        public override object ParseLiteral(IValue value)
        {
            if (value is StringValue stringValue)
            {
                return GeoPoint.TryParse(stringValue.Value)?.ToString() ?? base.ParseLiteral(value);
            }
            return base.ParseLiteral(value);
        }

        public override object ParseValue(object value)
        {
            if (value is string stringValue)
            {
                return GeoPoint.TryParse(stringValue)?.ToString() ?? base.ParseValue(value);
            }
            return base.ParseValue(value);
        }
    }
}
