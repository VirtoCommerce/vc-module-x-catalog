using System.Collections.Generic;
using GraphQL;
using GraphQL.Builders;
using GraphQL.Types;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Queries;
using static VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class Brand : Entity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
        public string Permalink { get; set; }
        public bool Featured { get; set; }
    }

    public class SearchBrandResponse : GenericSearchResult<Brand>
    {
    }

    public class BrandType : ObjectGraphType<Brand>
    {
        public BrandType()
        {
            Field(x => x.Id, nullable: false).Description("Brand ID.");
            Field(x => x.Name, true).Description("Brand name.");
            Field(x => x.Image, true).Description("Brand logo URL.");
            Field(x => x.Description, true).Description("Brand description.");
            Field(x => x.Featured, true).Description("Brand.");
        }
    }

    public class SearchBrandQuery : CatalogQueryBase<SearchBrandResponse>, ISearchQuery
    {
        public string Sort { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public string Keyword { get => Query; set => Query = value; }
        public string Query { get; set; }
        public string Filter { get; set; }

        public override IEnumerable<QueryArgument> GetArguments()
        {
            yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId));
            yield return Argument<StringGraphType>(nameof(UserId));
            yield return Argument<StringGraphType>(nameof(CurrencyCode));
            yield return Argument<StringGraphType>(nameof(CultureName));

            yield return Argument<StringGraphType>(nameof(Sort));
            yield return Argument<StringGraphType>(nameof(Query), "The query parameter performs the full-text search");
            yield return Argument<StringGraphType>(nameof(Filter), "This parameter applies a filter to the query results");
        }

        public override void Map(IResolveFieldContext context)
        {
            base.Map(context);

            Query = context.GetArgument<string>(nameof(Query));
            Filter = context.GetArgument<string>(nameof(Filter));
            Sort = context.GetArgument<string>(nameof(Sort));

            if (context is IResolveConnectionContext connectionContext)
            {
                Skip = int.TryParse(connectionContext.After, out var skip) ? skip : 0;
                Take = connectionContext.First ?? connectionContext.PageSize ?? Connections.DefaultPageSize;
            }
        }
    }
}
