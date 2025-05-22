using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using PipelineNet.Middleware;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.PricingModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

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
                    product.AllPrices = _mapper.Map<IEnumerable<ProductPrice>>(prices.Where(x => x.ProductId == product.Id), options =>
                    {
                        options.Items["all_currencies"] = parameter.AllStoreCurrencies;
                        options.Items["currency"] = parameter.Currency;
                        options.Items["pricelists"] = evalContext.Pricelists?.ToDictionary(x => x.Id);
                    }).ToList();

                    product.ApplyStaticDiscounts();
                }
            }

            if (responseGroup.HasFlag(ExpProductResponseGroup.LoadVariationPrices) && parameter.Results.Any())
            {
                foreach (var expProducts in parameter.Results)
                {
                    var minVariationPrices = _mapper.Map<IEnumerable<ProductPrice>>(expProducts.IndexedMinVariationPrices, options =>
                    {
                        options.Items["all_currencies"] = parameter.AllStoreCurrencies;
                        options.Items["currency"] = parameter.Currency;
                    }).ToList();

                    expProducts.MinVariationPrice = parameter.Currency != null
                        ? minVariationPrices.FirstOrDefault(x => x.Currency.Equals(parameter.Currency))
                        : minVariationPrices.FirstOrDefault();
                }
            }

            await next(parameter);
        }

        protected virtual async Task<PricingModule.Core.Model.PriceEvaluationContext> GetPriceEvaluationContext(SearchProductQuery query, Store store)
        {
            var evalContext = AbstractTypeFactory<PricingModule.Core.Model.PriceEvaluationContext>.TryCreateInstance();
            evalContext.Currency = query.CurrencyCode;
            evalContext.StoreId = query.StoreId;
            evalContext.CatalogId = store?.Catalog;
            evalContext.CustomerId = query.UserId;
            evalContext.Language = query.CultureName;
            evalContext.CertainDate = DateTime.UtcNow;

            await _pipeline.Execute(evalContext);

            return evalContext;
        }
    }
}
