using GraphQL.Types;
using VirtoCommerce.CatalogModule.Core.Model;
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
            Field(x => x.Name, nullable: false).Description("The name of the property group.");
            Field(x => x.Priority, nullable: true).Description("The display order of the property group.");

            Field<NonNullGraphType<StringGraphType>>("localizedName").Resolve(context =>
            {
                var cultureName = context.GetArgumentOrValue<string>("cultureName");
                var group = context.Source;
                var localizedName = group.LocalizedName?.GetValue(cultureName);
                if (!string.IsNullOrEmpty(localizedName))
                {
                    return localizedName;
                }
                return group.Name;
            }).Description("The localized name of the property group.");

            Field<NonNullGraphType<StringGraphType>>("localizedDescription").Resolve(context =>
            {
                var cultureName = context.GetArgumentOrValue<string>("cultureName");
                var group = context.Source;
                var localizedDescription = group.LocalizedDescription?.GetValue(cultureName);
                if (!string.IsNullOrEmpty(localizedDescription))
                {
                    return localizedDescription;
                }
                return null;
            }).Description("The localized description of the property group.");
        }
    }
}
