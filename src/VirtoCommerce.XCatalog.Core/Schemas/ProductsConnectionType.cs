using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;
using GraphQL.Types.Relay;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XCatalog.Core.Models;
using CoreFacets = VirtoCommerce.Xapi.Core.Schemas.Facets;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class ProductsConnectionType<TNodeType> : ConnectionType<TNodeType>
        where TNodeType : IGraphType
    {
        public ProductsConnectionType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CoreFacets.FilterFacetResultType>>>>("filter_facets")
                .Description("Filter facets")
                .Resolve(context => ((ProductsConnection<ExpProduct>)context.Source).Facets.OfType<FilterFacetResult>());

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CoreFacets.RangeFacetResultType>>>>("range_facets")
                .Description("Range facets")
                .Resolve(context => ((ProductsConnection<ExpProduct>)context.Source).Facets.OfType<RangeFacetResult>());

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CoreFacets.TermFacetResultType>>>>("term_facets")
                .Description("Term facets")
                .Resolve(context => ((ProductsConnection<ExpProduct>)context.Source).Facets.OfType<TermFacetResult>());

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<SearchProductFilterResultType>>>>("filters")
                .Description("Parsed filters")
                .Resolve(context => ((ProductsConnection<ExpProduct>)context.Source).Filters);
        }
    }

    public class ProductsConnection<TNode> : PagedConnection<TNode>
    {
        public ProductsConnection(IEnumerable<TNode> superset, int skip, int take, int totalCount)
            : base(superset, skip, take, totalCount)
        {
        }

        public IList<FacetResult> Facets { get; set; }

        public IList<SearchProductFilterResult> Filters { get; set; }
    }
}
