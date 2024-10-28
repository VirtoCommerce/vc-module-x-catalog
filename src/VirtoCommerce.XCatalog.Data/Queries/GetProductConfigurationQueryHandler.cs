using System.Collections.Generic;
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
                new ExpConfigurationSection
                {
                    Name = "Beverages",
                },
                new ExpConfigurationSection
                {
                    Name = "Baloons",
                },
                new ExpConfigurationSection
                {
                    Name = "Chips and Snacks"
                }
            }
        };

        foreach (var section in result.ConfigurationSections)
        {
            var productsRequest = new SearchProductQuery()
            {
                StoreId = request?.StoreId,
                CultureName = request?.CultureName,
                CurrencyCode = request?.CurrencyCode,
                UserId = request?.UserId,
                OrganizationId = request?.OrganizationId,
                Take = 3,
                Skip = 3 * result.ConfigurationSections.IndexOf(section),
            };

            var productsResponse = await _mediator.Send(productsRequest, cancellationToken);
            section.Products = productsResponse.Results;
        }

        return result;
    }
}
