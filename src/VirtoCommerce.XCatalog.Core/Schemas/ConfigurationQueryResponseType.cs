using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;

namespace VirtoCommerce.XCatalog.Core.Schemas;

public class ConfigurationQueryResponseType : ExtendableGraphType<ConfigurationQueryResponse>
{
    public ConfigurationQueryResponseType()
    {
        Field<ListGraphType<ConfigurationSectionType>>(
            nameof(ConfigurationQueryResponse.ConfigurationSections),
            resolve: context => context.Source.ConfigurationSections);
    }
}

public class ConfigurationSectionType : ExtendableGraphType<ExpConfigurationSection>
{
    public ConfigurationSectionType()
    {
        Field(x => x.Name, nullable: true).Description("Configuration section name");
        Field(x => x.Description, nullable: true).Description("Configuration section description");
        Field(x => x.IsRequired, nullable: false).Description("Is configuration section required");
        Field(x => x.Quantity, nullable: true);

        ExtendableField<ListGraphType<ProductType>>(
            nameof(ExpConfigurationSection.Products),
            resolve: context => context.Source.Products);
    }
}
