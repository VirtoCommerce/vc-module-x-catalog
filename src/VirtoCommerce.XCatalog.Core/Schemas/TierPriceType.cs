using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.XCatalog.Core.Schemas
{
    public class TierPriceType : ExtendableGraphType<TierPrice>
    {
        public TierPriceType()
        {
            Field<NonNullGraphType<MoneyType>>("price")
                .Description("Price")
                .Resolve(context => context.Source.ActualPrice);
            Field<NonNullGraphType<MoneyType>>("priceWithTax")
                .Description("Price with tax")
                .Resolve(context => context.Source.ActualPriceWithTax);
            Field<NonNullGraphType<LongGraphType>>("quantity")
                .Description("Quantity")
                .Resolve(context => context.Source.Quantity);
        }
    }
}
