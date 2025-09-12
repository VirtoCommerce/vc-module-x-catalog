namespace VirtoCommerce.XCatalog.Core.Models;

public class ProductPickupLocation
{
    public string Name { get; set; }
    public string Address { get; set; }
    public string AvailabilityType { get; set; }
    public string Note { get; set; }
    public long? AvailableQuantity { get; set; }
}
