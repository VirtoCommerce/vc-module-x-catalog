namespace VirtoCommerce.XCatalog.Core.Models;

public class ProductPickupLocation
{
    public string Name { get; set; }
    public string Address { get; set; }
    public string ShipmentType { get; set; }
    public int? ShipmentHours { get; set; }
    public int? AvailableQuantity { get; set; }
}
