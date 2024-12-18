using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class TierPriceType : ExtendableGraphType<TierPrice>
    {
        public TierPriceType()
        {
            Field<NonNullGraphType<MoneyType>>("price",
                "Price",
                resolve: context => context.Source.ActualPrice);
            Field<NonNullGraphType<MoneyType>>("priceWithTax",
                "Price with tax",
                resolve: context => context.Source.ActualPriceWithTax);
            Field<NonNullGraphType<LongGraphType>>("quantity",
                "Quantity",
                resolve: context => context.Source.Quantity);
        }
    }
}
