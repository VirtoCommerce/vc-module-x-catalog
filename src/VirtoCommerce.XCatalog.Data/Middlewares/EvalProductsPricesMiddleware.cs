using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using PipelineNet.Middleware;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.PricingModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using XapiProductPrice = VirtoCommerce.Xapi.Core.Models.ProductPrice;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    public class EvalProductsPricesMiddleware : IAsyncMiddleware<SearchProductResponse>
    {
        private readonly IMapper _mapper;
        private readonly IPricingEvaluatorService _pricingEvaluatorService;
        private readonly IGenericPipelineLauncher _pipeline;
        private readonly IStoreService _storeService;

        public EvalProductsPricesMiddleware(
            IMapper mapper,
            IOptionalDependency<IPricingEvaluatorService> pricingEvaluatorService,
            IGenericPipelineLauncher pipeline,
            IStoreService storeService)
        {
            _mapper = mapper;
            _pricingEvaluatorService = pricingEvaluatorService.Value;
            _pipeline = pipeline;
            _storeService = storeService;
        }

        public virtual async Task Run(SearchProductResponse parameter, Func<SearchProductResponse, Task> next)
        {
            ArgumentNullException.ThrowIfNull(parameter);

            var query = parameter.Query;
            if (query == null)
            {
                throw new OperationCanceledException("Query must be set");
            }

            if (_pricingEvaluatorService == null)
            {
                await next(parameter);
                return;
            }

            // If prices evaluation requested
            // Always evaluate prices with PricingEvaluatorService
            var responseGroup = EnumUtility.SafeParse(query.GetResponseGroup(), ExpProductResponseGroup.None);
            if (responseGroup.HasFlag(ExpProductResponseGroup.LoadPrices) && parameter.Results.Any())
            {
                // find Store by Id to get Catalog Id
                var store = await _storeService.GetNoCloneAsync(query.StoreId, StoreResponseGroup.StoreInfo.ToString());
                var evalContext = await GetPriceEvaluationContext(query, store);

                evalContext.ProductIds = parameter.Results.Select(x => x.Id).ToArray();
                var prices = await _pricingEvaluatorService.EvaluateProductPricesAsync(evalContext);

                foreach (var product in parameter.Results)
                {
                    product.AllPrices = MapPrices(prices.Where(x => x.ProductId == product.Id), parameter, evalContext);

                    product.ApplyStaticDiscounts();
                }
            }

            if (responseGroup.HasFlag(ExpProductResponseGroup.LoadVariationPrices) && parameter.Results.Any())
            {
                foreach (var expProduct in parameter.Results)
                {
                    var minVariationPrices = MapPrices(expProduct.IndexedMinVariationPrices, parameter);

                    expProduct.MinVariationPrice = parameter.Currency != null
                        ? minVariationPrices.FirstOrDefault(x => x.Currency.Equals(parameter.Currency))
                        : minVariationPrices.FirstOrDefault();
                }
            }

            await next(parameter);
        }

        protected virtual async Task<PriceEvaluationContext> GetPriceEvaluationContext(SearchProductQuery query, Store store)
        {
            var evalContext = AbstractTypeFactory<PriceEvaluationContext>.TryCreateInstance();
            evalContext.Currency = query.CurrencyCode;
            evalContext.StoreId = query.StoreId;
            evalContext.CatalogId = store?.Catalog;
            evalContext.CustomerId = query.UserId;
            evalContext.Language = query.CultureName;
            evalContext.CertainDate = DateTime.UtcNow;

            await _pipeline.Execute(evalContext);

            return evalContext;
        }

        private IList<XapiProductPrice> MapPrices(IEnumerable<Price> prices, SearchProductResponse parameter, PriceEvaluationContext evalContext = null)
        {
            return _mapper.Map<IEnumerable<XapiProductPrice>>(prices, options =>
            {
                options.Items["all_currencies"] = parameter.AllStoreCurrencies;
                options.Items["currency"] = parameter.Currency;

                if (evalContext != null && !evalContext.Pricelists.IsNullOrEmpty())
                {
                    options.Items["pricelists"] = evalContext.Pricelists.ToDictionary(x => x.Id);
                }
            }).ToList();
        }
    }
}
