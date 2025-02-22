using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using PipelineNet.Middleware;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.MarketingModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    public class EvalProductsDiscountsMiddleware : IAsyncMiddleware<SearchProductResponse>
    {
        private readonly IMapper _mapper;
        private readonly IMarketingPromoEvaluator _marketingEvaluator;
        private readonly IGenericPipelineLauncher _pipeline;

        public EvalProductsDiscountsMiddleware(
            IMapper mapper,
            IOptionalDependency<IMarketingPromoEvaluator> marketingEvaluator,
            IGenericPipelineLauncher pipeline)
        {
            _mapper = mapper;
            _marketingEvaluator = marketingEvaluator.Value;
            _pipeline = pipeline;
        }

        public virtual async Task Run(SearchProductResponse parameter, Func<SearchProductResponse, Task> next)
        {
            ArgumentNullException.ThrowIfNull(parameter);

            var query = parameter.Query;
            if (query == null)
            {
                throw new OperationCanceledException("Query must be set");
            }

            if (_marketingEvaluator == null)
            {
                await next(parameter);
                return;
            }

            var responseGroup = EnumUtility.SafeParse(query.GetResponseGroup(), ExpProductResponseGroup.None);
            // If promotion evaluation requested
            if (responseGroup.HasFlag(ExpProductResponseGroup.LoadPrices))
            {
                var promoEvalContext = await GetPromotionEvaluationContext(query);

                if (query.EvaluatePromotions)
                {
                    //Evaluate promotions
                    promoEvalContext.PromoEntries = parameter.Results.Select(x => _mapper.Map<ProductPromoEntry>(x, options =>
                    {
                        options.Items["all_currencies"] = parameter.AllStoreCurrencies;
                        options.Items["currency"] = parameter.Currency;
                    })).ToList();

                    var promotionResults = await _marketingEvaluator.EvaluatePromotionAsync(promoEvalContext);
                    var promoRewards = promotionResults.Rewards.OfType<CatalogItemAmountReward>().ToArray();
                    if (promoRewards.Length != 0)
                    {
                        parameter.Results.Apply(x => x.ApplyRewards(promoRewards));
                    }
                }
            }
            await next(parameter);
        }

        protected virtual async Task<PromotionEvaluationContext> GetPromotionEvaluationContext(SearchProductQuery query)
        {
            var promoEvalContext = AbstractTypeFactory<PromotionEvaluationContext>.TryCreateInstance();
            promoEvalContext.Currency = query.CurrencyCode;
            promoEvalContext.StoreId = query.StoreId;
            promoEvalContext.Language = query.CultureName;
            promoEvalContext.CustomerId = query.UserId;

            await _pipeline.Execute(promoEvalContext);

            return promoEvalContext;
        }
    }
}
