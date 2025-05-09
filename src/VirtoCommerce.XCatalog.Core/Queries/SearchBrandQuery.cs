using System.Collections.Generic;
using GraphQL;
using GraphQL.Builders;
using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;
using static VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Core.Queries
{
    public class SearchBrandQuery : CatalogQueryBase<SearchBrandResponse>, ISearchQuery
    {
        public string Sort { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public string Keyword { get; set; }

        public override IEnumerable<QueryArgument> GetArguments()
        {
            yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId));
            yield return Argument<StringGraphType>(nameof(UserId));
            yield return Argument<StringGraphType>(nameof(CurrencyCode));
            yield return Argument<StringGraphType>(nameof(CultureName));
            yield return Argument<StringGraphType>(nameof(Sort));
            yield return Argument<StringGraphType>(nameof(Keyword), "The query parameter performs the keyword search");
        }

        public override void Map(IResolveFieldContext context)
        {
            base.Map(context);

            Keyword = context.GetArgument<string>(nameof(Keyword));
            Sort = context.GetArgument<string>(nameof(Sort));

            if (context is IResolveConnectionContext connectionContext)
            {
                Skip = int.TryParse(connectionContext.After, out var skip) ? skip : 0;
                Take = connectionContext.First ?? connectionContext.PageSize ?? Connections.DefaultPageSize;
            }
        }
    }
}
