using GraphQL;
using GraphQL.Types;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class PropertyGroupType : ExtendableGraphType<PropertyGroup>
    {
        public PropertyGroupType()
        {
            Name = "PropertyGroup";
            Description = "Property group.";

            Field(x => x.Id, nullable: false).Description("The unique ID of the property group.");
            Field(x => x.DisplayOrder, nullable: true).Description("The display order of the property group.");

            Field<StringGraphType>("name")
                .Resolve(context => GetLocalizedValue(context, context.Source.LocalizedName, context.Source.Name))
                .Description("The localized name of the property group.");

            Field<StringGraphType>("description")
                .Resolve(context => GetLocalizedValue(context, context.Source.LocalizedDescription))
                .Description("The localized description of the property group.");
        }

        private static string GetLocalizedValue(IResolveFieldContext context, LocalizedString localizedString, string fallbackValue = null)
        {
            var cultureName = context.GetArgumentOrValue<string>("cultureName");
            var localizedValue = localizedString?.GetValue(cultureName);
            if (!string.IsNullOrEmpty(localizedValue))
            {
                return localizedValue;
            }
            return fallbackValue;
        }
    }
}
