using GraphQLParser.AST;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Schemas.ScalarTypes;

namespace VirtoCommerce.XCatalog.Core.Schemas.ScalarTypes
{
    public class PropertyValueGraphType : AnyValueGraphType
    {
        public override object ParseLiteral(GraphQLValue value)
        {
            if (value is GraphQLStringValue stringValue)
            {
                return GeoPoint.TryParse(stringValue.Value.ToString())?.ToString() ?? base.ParseLiteral(value);
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
