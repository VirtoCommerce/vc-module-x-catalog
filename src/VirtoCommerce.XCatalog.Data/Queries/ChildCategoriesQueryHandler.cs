using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class ChildCategoriesQueryHandler : IQueryHandler<ChildCategoriesQuery, ChildCategoriesQueryResponse>
{
    private readonly ICategoryTreeService _categoryTreeService;

    private readonly IMediator _mediator;

    public ChildCategoriesQueryHandler(
        ICategoryTreeService categoryTreeService,
        IMediator mediator)
    {
        _categoryTreeService = categoryTreeService;

        _mediator = mediator;
    }

    public virtual async Task<ChildCategoriesQueryResponse> Handle(ChildCategoriesQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<ChildCategoriesQueryResponse>.TryCreateInstance();

        if (request.Store is null)
        {
            return result;
        }

        var level = request.MaxLevel;
        var root = new ExpCategory { Key = request.CategoryId };
        var parents = new List<ExpCategory> { root };

        while (level > 0)
        {
            var parentIds = parents.Select(x => x.Key).ToList();
            var parentNodes = await _categoryTreeService.GetNodesWithChildren(request.Store.Catalog, parentIds, request.OnlyActive);

            foreach (var parent in parents)
            {
                var parentNode = parentNodes.FirstOrDefault(x => x.Id == parent.Key);
                parent.ChildCategories = parentNode?.ChildIds.Select(id => new ExpCategory { Key = id }).ToList() ?? new List<ExpCategory>();
            }

            parents = parents.SelectMany(x => x.ChildCategories).ToList();
            level--;
        }

        result.ChildCategories = root.ChildCategories ?? new List<ExpCategory>();

        // try resolve products via facets
        if (!string.IsNullOrEmpty(request.ProductFilter))
        {
            var outlineIds = await GetProductOutlineIds(request);
            if (outlineIds.Count != 0)
            {
                FilterChildCategories(result.ChildCategories, outlineIds);
            }
            else
            {
                result.ChildCategories = new List<ExpCategory>();
            }
        }

        return result;
    }

    private static void FilterChildCategories(IList<ExpCategory> categories, HashSet<string> outlines)
    {
        if (categories.IsNullOrEmpty())
        {
            return;
        }

        foreach (var category in categories.ToList())
        {
            if (!outlines.TryGetValue(category.Key, out var _))
            {
                categories.Remove(category);
            }
            else
            {
                FilterChildCategories(category.ChildCategories, outlines);
            }
        }
    }

    private async Task<HashSet<string>> GetProductOutlineIds(ChildCategoriesQuery childCategoriesQuery)
    {
        var result = new HashSet<string>();

        var productsRequest = AbstractTypeFactory<SearchProductQuery>.TryCreateInstance();
        productsRequest.StoreId = childCategoriesQuery?.StoreId;
        productsRequest.CultureName = childCategoriesQuery?.CultureName;
        productsRequest.CurrencyCode = childCategoriesQuery?.CurrencyCode;
        productsRequest.UserId = childCategoriesQuery?.UserId ?? ModuleConstants.AnonymousUser.UserName;
        productsRequest.OrganizationId = childCategoriesQuery?.OrganizationId;
        productsRequest.Filter = childCategoriesQuery?.ProductFilter;
        productsRequest.Take = 0;
        productsRequest.Facet = "__outline";
        productsRequest.IncludeFields =
        [
            "term_facets.name",
            "term_facets.label",
            "term_facets.terms.label",
            "term_facets.terms.term",
            "term_facets.terms.count",
        ];

        var productsResult = await _mediator.Send(productsRequest);

        var facetNames = new[] { "__outline" };
        var facets = productsResult.Facets.OfType<TermFacetResult>().Where(x => facetNames.Contains(x.Name));
        foreach (var facet in facets)
        {
            foreach (var term in facet.Terms)
            {
                var terms = term.Term.Split('/', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                result.AddRange(terms);
            }
        }

        return result;
    }
}
