using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Builders;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Types;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas.ScalarTypes;
using static VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class PropertyType : ExtendableGraphType<Property>
    {
        private readonly IMeasureService _measureService;

        public PropertyType(IMediator mediator, IDataLoaderContextAccessor dataLoader, IMeasureService measureService, IPropertyGroupService propertyGroupService)
        {
            _measureService = measureService;

            Name = "Property";
            Description = "Products attributes.";

            Field("id", x => x.Id ?? x.Name, nullable: false).Description("The unique ID of the property.");

            Field(x => x.Name, nullable: false).Description("The name of the property.");

            Field(x => x.Hidden, nullable: false).Description("Is property hidden.");

            Field(x => x.Multivalue, nullable: false).Description("Is property has multiple values.");

            Field(x => x.DisplayOrder, nullable: true).Description("The display order of the property.");

            Field<NonNullGraphType<StringGraphType>>("label")
                .Resolve(context =>
                {
                    var cultureName = context.GetValue<string>("cultureName");

                    var label = cultureName != null
                        ? context.Source.DisplayNames
                            ?.FirstOrDefault(x => x.LanguageCode.EqualsIgnoreCase(cultureName))
                            ?.Name
                        : default;

                    return string.IsNullOrWhiteSpace(label)
                        ? context.Source.Name
                        : label;
                });

            Field<NonNullGraphType<PropertyTypeEnum>>("propertyType")
                .Resolve(context => context.Source.Type);

            Field<NonNullGraphType<PropertyValueTypeEnum>>("propertyValueType")
                // since PropertyType is used both for property metadata queries and product/category/catalog queries
                // to infer "valueType" need to look in ValueType property in case of metadata query or in the first value in case
                // when the Property object was created dynamically by grouping
                .Resolve(context => context.Source.Values.IsNullOrEmpty()
                    ? context.Source.ValueType
                    : context.Source.Values.Select(x => x.ValueType).First())
                // Values.IsNullOrEmpty() is false here. It means at least one element is present
                .Description("ValueType of the property.");

            Field<PropertyValueGraphType>("value")
                .ResolveAsync(context => ResolveValue(context.Source, context.GetCultureName()));

            Field<IntGraphType>("valueDisplayOrder")
                .Resolve(context => context.Source.Values.FirstOrDefault()?.DisplayOrder)
                .Description("The display order of the value.");

            Field<StringGraphType>("valueId")
                .Resolve(context => context.Source.Values.Select(x => x.ValueId).FirstOrDefault());

            var group = new FieldType
            {
                Name = "group",
                Type = GraphTypeExtensionHelper.GetActualType<PropertyGroupType>(),
                Resolver = new FuncFieldResolver<Property, IDataLoaderResult<PropertyGroup>>(context =>
                {
                    var loader = dataLoader.Context.GetOrAddBatchLoader<string, PropertyGroup>("propertyGroupLoader", async (ids) =>
                    {
                        var groups = await propertyGroupService.GetNoCloneAsync(ids.ToList());
                        return groups.ToDictionary(x => x.Id);
                    });
                    return loader.LoadAsync(context.Source.PropertyGroupId);
                })
            };
            AddField(group);

            Connection<PropertyDictionaryItemType>("propertyDictionaryItems")
                .PageSize(Connections.DefaultPageSize)
                .ResolveAsync(context => ResolveConnectionAsync(mediator, context));
        }

        protected virtual async Task<object> ResolveValue(Property source, string languageCode)
        {
            var propertyValue = source.Values.FirstOrDefault();
            if (source.ValueType != PropertyValueType.Measure || string.IsNullOrEmpty(source.MeasureId) || string.IsNullOrEmpty(propertyValue?.UnitOfMeasureId))
            {
                return source.Values.Select(x => x.Value).FirstOrDefault();
            }

            var measure = await _measureService.GetByIdAsync(source.MeasureId);
            if (measure == null)
            {
                return propertyValue.Value;
            }

            var symbol = string.Empty;
            var valueUnit = measure.Units.FirstOrDefault(x => x.Id == propertyValue.UnitOfMeasureId);
            var decimalValue = (decimal)propertyValue.Value;

            if (valueUnit != null)
            {
                symbol = valueUnit.LocalizedSymbol?.GetValue(languageCode) ?? valueUnit.Symbol;
            }

            return string.IsNullOrEmpty(symbol)
                ? decimalValue.FormatDecimal(languageCode)
                : $"{decimalValue.FormatDecimal(languageCode)} {symbol}";
        }

        private static async Task<object> ResolveConnectionAsync(IMediator mediator, IResolveConnectionContext<Property> context)
        {
            var first = context.First;

            _ = int.TryParse(context.After, out var skip);

            var query = new SearchPropertyDictionaryItemQuery
            {
                Skip = skip,
                Take = first ?? context.PageSize ?? Connections.DefaultPageSize,
                PropertyIds = [context.Source.Id],
            };

            var response = await mediator.Send(query);

            return new PagedConnection<PropertyDictionaryItem>(response.Result.Results, query.Skip, query.Take, response.Result.TotalCount);
        }
    }
}
