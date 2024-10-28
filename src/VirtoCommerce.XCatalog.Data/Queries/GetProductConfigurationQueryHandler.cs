using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XDigitalCatalog.Queries;

namespace VirtoCommerce.XCatalog.Data.Queries;

public class GetProductConfigurationQueryHandler : IQueryHandler<GetProductConfigurationQuery, ConfigurationQueryResponse>
{
    private readonly IMediator _mediator;

    private const string _productsFieldName = $"{nameof(ConfigurationQueryResponse.ConfigurationSections)}.{nameof(ExpConfigurationSection.Products)}.";

    public GetProductConfigurationQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<ConfigurationQueryResponse> Handle(GetProductConfigurationQuery request, CancellationToken cancellationToken)
    {
        var result = new ConfigurationQueryResponse
        {
            ConfigurationSections = new List<ExpConfigurationSection>
            {
                new()
                {
                    Name = "Beverages",
                    IsRequired = true,
                },
                new()
                {
                    Name = "Baloons",
                },
                new()
                {
                    Name = "Chips and Snacks"
                }
            }
        };


        var includeFields = request.IncludeFields
                .Where(x => x.StartsWith(_productsFieldName, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Replace(_productsFieldName, string.Empty, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var productsRequest = new SearchProductQuery()
        {
            StoreId = request?.StoreId,
            CultureName = request?.CultureName,
            CurrencyCode = request?.CurrencyCode,
            UserId = request?.UserId,
            OrganizationId = request?.OrganizationId,
            IncludeFields = includeFields,
            Take = 3,
        };

        foreach (var section in result.ConfigurationSections)
        {
            productsRequest.Skip = 3 * result.ConfigurationSections.IndexOf(section);

            var productsResponse = await _mediator.Send(productsRequest, cancellationToken);
            section.Products = productsResponse.Results;
        }

        return result;
    }
}
