//using MediatR;
//using Microsoft.AspNetCore.Authorization;
//using VirtoCommerce.CoreModule.Core.Currency;
//using VirtoCommerce.StoreModule.Core.Services;
//using VirtoCommerce.XCatalog.Core.Models;
//using VirtoCommerce.XCatalog.Core.Queries;
//using VirtoCommerce.XCatalog.Core.Schemas;

//namespace VirtoCommerce.XCatalog.Data.Queries;
//public class GetProductConfigurationQueryBuilder : CatalogQueryBuilder<GetProductConfigurationQuery, ConfigurationQueryResponse, ConfigurationQueryResponseType>
//{
//    public GetProductConfigurationQueryBuilder(IMediator mediator, IAuthorizationService authorizationService, IStoreService storeService, ICurrencyService currencyService)
//        : base(mediator, authorizationService, storeService, currencyService)
//    {
//    }

//    protected override string Name => "productConfiguration";
//}
