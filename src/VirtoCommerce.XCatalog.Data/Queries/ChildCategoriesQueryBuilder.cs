using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.CatalogModule.Core.Extensions;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.XCatalog.Core.Authorization;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class ChildCategoriesQueryBuilder : CatalogQueryBuilder<ChildCategoriesQuery, ChildCategoriesQueryResponse, ChildCategoriesQueryResponseType>
{
    private const int _batchSize = 100;
    private readonly ICategoryService _categoryService;
    private readonly ICatalogService _catalogService;

    protected override string Name => "ChildCategories";

    public ChildCategoriesQueryBuilder(
        IMediator mediator,
        IAuthorizationService authorizationService,
        IStoreService storeService,
        ICurrencyService currencyService,
        ICategoryService categoryService,
        ICatalogService catalogService)
        : base(mediator, authorizationService, storeService, currencyService)
    {
        _categoryService = categoryService;
        _catalogService = catalogService;
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, ChildCategoriesQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        await Authorize(context, request.Store, new CanAccessStoreAuthorizationRequirement());
    }

    protected override async Task AfterMediatorSend(IResolveFieldContext<object> context, ChildCategoriesQuery request, ChildCategoriesQueryResponse response)
    {
        await base.AfterMediatorSend(context, request, response);

        var categoryIds = new HashSet<string>();
        var root = new ExpCategory { ChildCategories = response.ChildCategories };

        foreach (var category in root.Traverse(x => x.ChildCategories).Where(x => x.Key != null))
        {
            categoryIds.Add(category.Key);
        }

        if (categoryIds.Count != 0)
        {
            var responseGroup = GetCategoryResponseGroup(context, request, response);
            var categoriesByIds = new Dictionary<string, Category>();
            var store = request.Store;
            var storeCatalog = await _catalogService.GetByIdAsync(store.Catalog);

            foreach (var idsBatch in categoryIds.Paginate(_batchSize))
            {
                IEnumerable<Category> categories = await _categoryService.GetAsync(idsBatch, responseGroup);
                categories = categories.FilterLinked(store, storeCatalog);
                categoriesByIds.AddRange(categories.ToDictionary(x => x.Id));
            }

            foreach (var category in root.Traverse(x => x.ChildCategories))
            {
                category.Category ??= categoriesByIds.GetValueSafe(category.Key);
            }
        }

        // Cannot reassign the root.ChildCategories because it is referenced in the response.
        RemoveUnsuitableCategories(root);
    }

    private void RemoveUnsuitableCategories(ExpCategory root)
    {
        if (root.ChildCategories == null)
        {
            return;
        }
        var itemsToRemove = root.ChildCategories.Where(x => x.Category == null).ToList();

        foreach (var category in itemsToRemove)
        {
            root.ChildCategories.Remove(category);
        }

        foreach (var category in root.ChildCategories)
        {
            RemoveUnsuitableCategories(category);
        }
    }

    protected virtual string GetCategoryResponseGroup(IResolveFieldContext<object> context, ChildCategoriesQuery request, ChildCategoriesQueryResponse response)
    {
        var searchCategoryQuery = context.GetCatalogQuery<SearchCategoryQuery>();
        searchCategoryQuery.IncludeFields = request.IncludeFields;

        return searchCategoryQuery.GetCategoryResponseGroup();
    }
}
