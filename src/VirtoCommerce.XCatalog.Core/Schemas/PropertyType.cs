using System.Linq;
using System.Threading.Tasks;
using GraphQL.Builders;
using GraphQL.DataLoader;
using GraphQL.Types;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas.ScalarTypes;
using static VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class PropertyType : ObjectGraphType<Property>
    {
        public PropertyType(IMediator mediator, IDataLoaderContextAccessor dataLoader)
        {
            Name = "Property";
            Description = "Products attributes.";

            Field("id", x => x.Id ?? x.Name, nullable: false).Description("The unique ID of the property.");

            Field(x => x.Name, nullable: false).Description("The name of the property.");

            Field(x => x.Hidden, nullable: false).Description("Is property hidden.");

            Field(x => x.Multivalue, nullable: false).Description("Is property has multiple values.");

            Field(x => x.DisplayOrder, nullable: true).Description("The display order of the property.");

            Field<NonNullGraphType<StringGraphType>>(
                "label",
                resolve: context =>
                {
                    var cultureName = context.GetValue<string>("cultureName");

                    var label = cultureName != null
                        ? context.Source.DisplayNames
                            ?.FirstOrDefault(x => x.LanguageCode.EqualsInvariant(cultureName))
                            ?.Name
                        : default;

                    return string.IsNullOrWhiteSpace(label)
                        ? context.Source.Name
                        : label;
                });

            Field<NonNullGraphType<StringGraphType>>(
                "type",
                resolve: context => context.Source.Type.ToString(),
                deprecationReason: "Use propertyType instead."
            );

            Field<NonNullGraphType<PropertyTypeEnum>>(
                "propertyType",
                resolve: context => context.Source.Type
            );

            Field<NonNullGraphType<StringGraphType>>(
                "valueType",
                // since PropertyType is used both for property metadata queries and product/category/catalog queries
                // to infer "valueType" need to look in ValueType property in case of metadata query or in the first value in case
                // when the Property object was created dynamically by grouping
                resolve: context => context.Source.Values.IsNullOrEmpty()
                        ? context.Source.ValueType.ToString()
                        : context.Source.Values.Select(x => x.ValueType).First().ToString(), // Values.IsNullOrEmpty() is false here. It means at least one element is present
                description: "ValueType of the property.",
                deprecationReason: "Use propertyValueType instead.");

            Field<NonNullGraphType<PropertyValueTypeEnum>>(
                "propertyValueType",
                // since PropertyType is used both for property metadata queries and product/category/catalog queries
                // to infer "valueType" need to look in ValueType property in case of metadata query or in the first value in case
                // when the Property object was created dynamically by grouping
                resolve: context => context.Source.Values.IsNullOrEmpty()
                    ? context.Source.ValueType
                    : context.Source.Values.Select(x => x.ValueType).First(), // Values.IsNullOrEmpty() is false here. It means at least one element is present
                description: "ValueType of the property.");

            Field<PropertyValueGraphType>(
                "value",
                resolve: context => context.Source.Values.Select(x => x.Value).FirstOrDefault()
            );

            Field<StringGraphType>(
                "valueId",
                resolve: context => context.Source.Values.Select(x => x.ValueId).FirstOrDefault()
            );

            Connection<PropertyDictionaryItemType>()
                .Name("propertyDictItems")
                .DeprecationReason("Use propertyDictionaryItems instead.")
                .PageSize(Connections.DefaultPageSize)
                .ResolveAsync(async context =>
                {
                    return await ResolveConnectionAsync(mediator, context);
                });

            Connection<PropertyDictionaryItemType>()
                .Name("propertyDictionaryItems")
                .PageSize(Connections.DefaultPageSize)
                .ResolveAsync(async context =>
                {
                    return await ResolveConnectionAsync(mediator, context);
                });

        }

        private static async Task<object> ResolveConnectionAsync(IMediator mediator, IResolveConnectionContext<Property> context)
        {
            var first = context.First;

            int.TryParse(context.After, out var skip);

            var query = new SearchPropertyDictionaryItemQuery
            {
                Skip = skip,
                Take = first ?? context.PageSize ?? Connections.DefaultPageSize,
                PropertyIds = new[] { context.Source.Id }
            };

            var response = await mediator.Send(query);

            return new PagedConnection<PropertyDictionaryItem>(response.Result.Results, query.Skip, query.Take, response.Result.TotalCount);
        }
    }
}
