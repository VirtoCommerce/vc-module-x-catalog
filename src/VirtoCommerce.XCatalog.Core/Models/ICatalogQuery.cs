using VirtoCommerce.Xapi.Core.Index;

namespace VirtoCommerce.XCatalog.Core.Models
{
    public interface ICatalogQuery : IHasIncludeFields
    {
        string StoreId { get; set; }
        string UserId { get; set; }
        string OrganizationId { get; set; }
        string CultureName { get; set; }
        string CurrencyCode { get; set; }
        string PreviousBreadcrumbsPath { get; set; }
    }
}
