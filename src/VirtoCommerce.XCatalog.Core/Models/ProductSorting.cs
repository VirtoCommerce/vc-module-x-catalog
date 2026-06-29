namespace VirtoCommerce.XCatalog.Core.Models
{
    /// <summary>
    /// A sort ordering ("sort by" option) exposed to the storefront on the products connection. Projected from the
    /// catalog module's effective orderings for the current store, with the name resolved for the request culture.
    /// </summary>
    public class ProductSorting
    {
        /// <summary>Stable code used as the <c>?sort=&lt;id&gt;</c> value.</summary>
        public string Id { get; set; }

        /// <summary>Culture-resolved display name.</summary>
        public string Name { get; set; }

        /// <summary>Whether this is the store default ordering (applied when no sort is specified).</summary>
        public bool IsDefault { get; set; }

        /// <summary>Whether this ordering is the one applied to the current result.</summary>
        public bool IsSelected { get; set; }
    }
}
