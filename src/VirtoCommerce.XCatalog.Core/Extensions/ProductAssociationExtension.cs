using System.Threading.Tasks;
using GraphQL;
using GraphQL.Builders;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Core.Extensions
{
    public static class ProductAssociationExtension
    {
        /// <summary>
        /// Resolves the GraphQL associations connection for any product-like source (a product or its variation),
        /// searching associations by the source item's own id. Shared by ProductType and VariationType.
        /// </summary>
        public static async Task<object> ResolveAssociationsConnectionAsync<TSource>(this IResolveConnectionContext<TSource> context)
            where TSource : ExpProduct
        {
            var first = context.First;

            int.TryParse(context.After, out var skip);

            var query = new SearchProductAssociationsQuery
            {
                Skip = skip,
                Take = first ?? context.PageSize ?? 10,

                Keyword = context.GetArgument<string>("query"),
                Group = context.GetArgument<string>("group"),
                ObjectIds = [context.Source.IndexedProduct.Id]
            };

            var response = await context.GetMediator().Send(query);

            return new PagedConnection<ProductAssociation>(response.Result.Results, query.Skip, query.Take, response.Result.TotalCount);
        }
    }
}
