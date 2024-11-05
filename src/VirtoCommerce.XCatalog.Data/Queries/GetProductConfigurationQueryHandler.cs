using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XDigitalCatalog.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetProductConfigurationQueryHandler : IQueryHandler<GetProductConfigurationQuery, ConfigurationQueryResponse>
{
    private readonly IMediator _mediator;
    private readonly IConfigurableProductService _configurableProductService;

    private const string _productsFieldName = $"{nameof(ConfigurationQueryResponse.ConfigurationSections)}.{nameof(ExpConfigurationSection.Products)}.";

    public GetProductConfigurationQueryHandler(IConfigurableProductService configurableProductService, IMediator mediator)
    {
        _configurableProductService = configurableProductService;
        _mediator = mediator;
    }

    public async Task<ConfigurationQueryResponse> Handle(GetProductConfigurationQuery request, CancellationToken cancellationToken)
    {
        var configuration = await _configurableProductService.GetProductConfigurationAsync(request.ProductId);

        var result = new ConfigurationQueryResponse
        {
            ConfigurationSections = configuration.ConfigurationSections.Select(x => new ExpConfigurationSection
            {
                Id = x.Id,
                Name = x.Name,
                IsRequired = x.IsRequired,
                Description = x.Description,
                Quantity = x.Quantity,
                Type = x.Type,
                ProductIds = x.ProductIds,
            }).ToList()
        };

        var includeFields = request.IncludeFields
                .Where(x => x.StartsWith(_productsFieldName, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Replace(_productsFieldName, string.Empty, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var productsRequest = new SearchProductQuery
        {
            StoreId = request.StoreId,
            CultureName = request.CultureName,
            CurrencyCode = request.CurrencyCode,
            UserId = request.UserId,
            OrganizationId = request.OrganizationId,
            IncludeFields = includeFields,
            ObjectIds = result.ConfigurationSections.SelectMany(x => x.ProductIds).Distinct().ToArray()
        };

        var productsResponse = await _mediator.Send(productsRequest, cancellationToken);
        var productsByIds = productsResponse.Results.ToDictionary(x => x.Id, x => x);

        foreach (var section in result.ConfigurationSections)
        {
            foreach (var productId in section.ProductIds)
            {
                if (productsByIds.TryGetValue(productId, out var product))
                {
                    section.Products.Add(product);
                }
            }
        }

        return result;
    }
}
