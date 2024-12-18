using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Types;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class ProductAssociationType : ExtendableGraphType<ProductAssociation>
    {
        public ProductAssociationType(IDataLoaderContextAccessor dataLoader, IMediator mediator)
        {
            Name = "ProductAssociation";
            Description = "product association.";

            Field(d => d.Type, nullable: false);
            Field(d => d.Priority, nullable: false);
            Field(x => x.Quantity, nullable: true);
            Field(d => d.AssociatedObjectId, nullable: true);
            Field(d => d.AssociatedObjectType, nullable: true);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>("tags", resolve: context => context.Source.Tags?.ToList() ?? []);

            var productField = new FieldType
            {
                Name = "product",
                Type = GraphTypeExtensionHelper.GetActualType<ProductType>(),
                Resolver = new FuncFieldResolver<ProductAssociation, IDataLoaderResult<ExpProduct>>(context =>
                {
                    var loader = dataLoader.Context.GetOrAddBatchLoader<string, ExpProduct>("associatedProductLoader", (ids) => LoadProductsAsync(mediator, ids, context));
                    return loader.LoadAsync(context.Source.AssociatedObjectId);
                })
            };
            AddField(productField);
        }

        public static async Task<IDictionary<string, ExpProduct>> LoadProductsAsync(IMediator mediator, IEnumerable<string> ids, IResolveFieldContext context)
        {
            var query = context.GetCatalogQuery<LoadProductsQuery>();
            query.ObjectIds = ids.ToArray();
            query.IncludeFields = context.SubFields.Values.GetAllNodesPaths(context).Select(x => x.Replace("associations.items.product", string.Empty)).ToArray();

            var response = await mediator.Send(query);
            return response.Products.ToDictionary(x => x.Id);
        }
    }
}
