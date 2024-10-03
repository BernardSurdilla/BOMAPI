
namespace CRUDFI.Models
{
    public class Order
    {

        public Guid? orderId { get; set; }
        public Guid suborderId { get; set; }
        public string pastryId { get; set; }
        public Guid? customerId { get; set; }
        public string customerName { get; set; } = string.Empty;

        public string? employeeName { get; set; } = string.Empty;

        public string designName { get; set; } = string.Empty;

        public Guid designId { get; set; }

        public Guid? employeeId { get; set; }

        public bool isActive { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public double price { get; set; }

        public string type { get; set; } = "";

        public int quantity { get; set; } //how many orders does he have

        public string status { get; set; } = ""; //ongoing, delayed, cancelled

        public DateTime createdAt { get; set; } //when it was ordered

        public string? lastUpdatedBy { get; set; } = ""; //confirmed or not

        public DateTime? lastUpdatedAt { get; set; } //when was confirmed or not,, picked up or not

        public string? description { get; set; }

        public string size { get; set; }

        public string flavor { get; set; }
    }

    public class CalendarFull
    {
        public Guid suborderId { get; set; }
        public Guid designId { get; set; }
        public Guid customerId { get; set; }
        public string customerName { get; set; }
        public string status { get; set; }
        public string designName { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public double price { get; set; } 
        public int quantity { get; set; }
        public string description { get; set; }
        public string flavor { get; set; }
        public string size { get; set; }
        public string payment { get; set; } = "";
        public DateTime? pickupDateTime { get; set; }
    }

    public class OrderResponse //for daily and weekly
    {
        public string day { get; set; }
        public int totalOrders { get; set; }
    }

    public class MonthOrdersResponse
    {
        public int day { get; set; }
        public int totalOrders { get; set; }
    }

    public class YearOrdersResponse
    {
        public string month { get; set; }
        public int totalOrders { get; set; }
    }

    public class ManualOrder
    {
        public string DesignName { get; set; }
        public decimal Price { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public int quantity { get; set; }
        public string cover { get; set; } = "";
        public string Description { get; set; }
        public string size { get; set; }
        public string flavor { get; set; }
        public string picture { get; set; }
        public string message { get; set; }
        public string type { get; set; }
        public string PickupDate { get; set; } = "yyyy-mm-dd";
        public string PickupTime { get; set; } = "hh:mm AM/PM";
    }
    public class Custom
    {
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public int quantity { get; set; }
        public string customerName { get; set; } = string.Empty;
        public string cover { get; set; } = "";
        public string description { get; set; }
        public string size { get; set; }
        public string flavor { get; set; }
        public byte[] picture { get; set; }
        public string pickupDate { get; set; } = "yyyy-mm-dd";
        public string pickupTime { get; set; } = "hh:mm AM/PM";
        public string message { get; set; }
        public string type { get; set; }
    }
    public class PostCustomOrder
    {
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public int quantity { get; set; }
        public string cover { get; set; } = "";
        public string description { get; set; }
        public string size { get; set; }
        public string flavor { get; set; }
        public string picture { get; set; }
        public string message { get; set; }
        public string type { get; set; }
        public string pickupDate { get; set; } = "yyyy-mm-dd";
        public string pickupTime { get; set; } = "hh:mm AM/PM";
    }
    public class CustomOrderUpdateRequest
    {
        public string designName { get; set; }
        public decimal price { get; set; }
    }

    public class CustomPartial
    {
        public Guid customId { get; set; }
        public Guid? orderId { get; set; }
        public Guid designId { get; set; }
        public Guid customerId { get; set; }
        public string customerName { get; set; }
        public DateTime createdAt { get; set; }
        public string? designName { get; set; }
        public double? Price { get; set; }
        public int quantity { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public string cover { get; set; } = "";
        public string description { get; set; }
        public string size { get; set; }
        public string flavor { get; set; }
        public string picture { get; set; }
        public string message { get; set; }
        public string type { get; set; }
        public Guid? employeeId { get; set; }
        public string? employeeName { get; set; } = string.Empty;
    }
    public class CustomOrderFull
    {
        public Guid customId { get; set; }
        public Guid? orderId { get; set; }
        public Guid designId { get; set; }
        public Guid customerId { get; set; }
        public string customerName { get; set; }
        public DateTime createdAt { get; set; }
        public string? designName { get; set; }
        public double? price { get; set; }
        public int quantity { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public string cover { get; set; } = "";
        public string description { get; set; }
        public string size { get; set; }
        public string flavor { get; set; }
        public string picture { get; set; }
        public string message { get; set; }
        public string type { get; set; }
        public Guid? employeeId { get; set; }
        public string employeeName { get; set; } = string.Empty;
        public string payment { get; set; } = "";
        public DateTime? pickupDateTime { get; set; }
    }

    public class UpdateOrderDetailsRequest
    {
        public string description { get; set; }
        public int quantity { get; set; }
        public string size { get; set; }
        public string flavor { get; set; }
        public string color { get; set; }
        public string shape { get; set; }
    }
    public class AdminInitial
    {
        public Guid? customId { get; set; }
        public Guid? orderId { get; set; }
        public Guid designId { get; set; }
        public Guid customerId { get; set; }
        public string customerName { get; set; }
        public DateTime createdAt { get; set; }
        public string payment { get; set; }
        public string status { get; set; }
        public string type { get; set; }
        public string designName { get; set; }
        public DateTime? pickup { get; set; }
        public string lastUpdatedBy { get; set; } = "";
        public DateTime? lastUpdatedAt { get; set; }
        public bool isActive { get; set; }
    }
    public class CustomerInitial
    {
        public Guid? customId { get; set; }
        public Guid? orderId { get; set; }
        public Guid designId { get; set; }
        public DateTime createdAt { get; set; }
        public string payment { get; set; }
        public string status { get; set; }
        public string type { get; set; }
        public string designName { get; set; }
        public Prices price { get; set; }
        public DateTime? pickup { get; set; }
    }

    public class Prices
    {
        public double full { get; set; }
        public double half { get; set;}
    }

    public class toPayInitial
    {
        public Guid suborderId { get; set; }
        public Guid? Id { get; set; }
        public Guid designId { get; set; }
        public Guid customerId { get; set; }
        public string customerName { get; set; }
        public DateTime createdAt { get; set; }
        public string pastryId { get; set; }
        public string designName { get; set; }
        public double price { get; set; }
        public int quantity { get; set; }
        public string payment { get; set; } = "";
        public DateTime? pickupDateTime { get; set; }
        public string lastUpdatedBy { get; set; } = "";
        public DateTime? lastUpdatedAt { get; set; }
        public bool isActive { get; set; }
    }
    public class Full
    {
        public Guid suborderId { get; set; }
        public Guid? orderId { get; set; }
        public Guid designId { get; set; }
        public Guid customerId { get; set; }
        public Guid? employeeId { get; set; }
        public string employeeName { get; set; } = string.Empty;
        public string customerName { get; set; }
        public DateTime createdAt { get; set; }
        public string pastryId { get; set; }
        public string status { get; set; }
        public string designName { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public double price { get; set; }
        public int quantity { get; set; }
        public string description { get; set; }
        public string flavor { get; set; }
        public string size { get; set; }
        public string payment { get; set; } = "";
        public DateTime? pickupDateTime { get; set; }
        public string lastUpdatedBy { get; set; } = "";
        public DateTime? lastUpdatedAt { get; set; }
        public bool isActive { get; set; }
    }
    public class OrderDetails
    {
        public Guid? orderId { get; set; }
        public string status { get; set; }
        public string payment { get; set; } = "";
        public string type { get; set; } = "";
        public DateTime? pickupDateTime { get; set; }

    }

    public class Cart
    {
        public Guid suborderId { get; set; }
        public Guid designId { get; set; }
        public string pastryId { get; set; }
        public string status { get; set; }
        public string designName { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public double price { get; set; }
        public int quantity { get; set; }
        public string? description { get; set; }
        public string flavor { get; set; }
        public string size { get; set; }
    }

    public class Artist
    {
        public Guid suborderId { get; set; }
        public Guid? orderId { get; set; }
        public Guid designId { get; set; }
        public Guid customerId { get; set; }
        public Guid? employeeId { get; set; }
        public string employeeName { get; set; } = string.Empty;
        public string customerName { get; set; }
        public DateTime createdAt { get; set; }
        public string pastryId { get; set; }
        public string status { get; set; }
        public string designName { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public double price { get; set; }
        public int quantity { get; set; }
        public string description { get; set; }
        public string flavor { get; set; }
        public string size { get; set; }
        public string lastUpdatedBy { get; set; } = "";
        public DateTime? lastUpdatedAt { get; set; }
        public bool isActive { get; set; }
    }
    public class CheckOutDetails
    {
        public Guid orderId { get; set; }
        public string status { get; set; }
        public string paymentMethod { get; set; }
        public string orderType { get; set; }
        public DateTime? pickupDateTime { get; set; }
        public double orderTotal { get; set; }

        // List to hold all suborders
        public List<OrderItem> orderItems { get; set; } = new List<OrderItem>();
    }

    public class OrderItem
    {
        public Guid suborderId { get; set; }
        public Guid orderId { get; set; }
        public Guid customerId { get; set; }
        public Guid employeeId { get; set; }
        public string employeeName { get; set; }
        public DateTime createdAt { get; set; }
        public string status { get; set; }
        public string pastryId { get; set; }
        public string color { get; set; }
        public string shape { get; set; }
        public Guid designId { get; set; }
        public string designName { get; set; }
        public double price { get; set; }
        public int quantity { get; set; }
        public string lastUpdatedBy { get; set; }
        public DateTime? lastUpdatedAt { get; set; }
        public bool isActive { get; set; }
        public string description { get; set; }
        public string flavor { get; set; }
        public string size { get; set; }
        public string customerName { get; set; }
        public double subOrderTotal { get; set; }

        // List to hold all add-ons for the order item
        public List<OrderAddon1> orderAddons { get; set; } = new List<OrderAddon1>();
    }
    public class AssignEmp
    {
        public string employeeId { get; set; }
    }

    public class OrderAddon1
    {
        public int id { get; set; }
        public string name { get; set; }
        public int quantity { get; set; }
        public double price { get; set; }
        public double addOnTotal { get; set; }
    }

    public class BuyNow
    {
        public string type { get; set; }
        public string pickupDate { get; set; } = "yyyy-mm-dd";
        public string pickupTime { get; set; } = "hh:mm AM/PM";
        public string payment { get; set; }
        public List<OrderDTO> orderItem { get; set; } = new List<OrderDTO>();
    }

    public class OrderDTO
    {
        public int quantity { get; set; }
        public string designId { get; set; }
        public string? description { get; set; }
        public string flavor { get; set; }
        public string size { get; set; }
        public string color { get; set; }
        public List<AddOn> addonItem { get; set; }
    }

    public class CheckOutRequest
    {
        public string type { get; set; }
        public string pickupDate { get; set; } = "yyyy-mm-dd";
        public string pickupTime { get; set; } = "hh:mm AM/PM";
        public string payment { get; set; }
        public List<Guid> suborderIds { get; set; }
    }


    public class SuborderResponse
    {
        public string suborderId { get; set; }
        public List<int> addonId { get; set; }
    }
    public class TotalOrders
    {
        public int total { get; set; }
    }
    public class forSales
    {
        public Guid saleId { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public double cost { get; set; }
        public string contact { get; set; }
        public int total { get; set; }
        public DateTime date { get; set; }
    }

    public class AddOnDSOS
    {
        public string addOnName { get; set; }
        public double price { get; set; }
        public int id { get; set; }
    }

    public class AddOnDS2
    {
        public int id { get; set; }
        public string addOnName { get; set; }
        public string? measurement { get; set; }
        public double price { get; set; }
        public double? size { get; set; }
        public DateTime dateAdded { get; set; }
        public DateTime? lastModifiedDate { get; set; }
    }

    public class AddOns
    {
        public int addOnsId { get; set; }
        public string name { get; set; }
        public double pricePerUnit { get; set; }
        public int quantity { get; set; }
        public double size { get; set; }
        public DateTime dateAdded { get; set; }
        public DateTime? lastModifiedDate { get; set; }
        public bool isActive { get; set; }
    }
    public class AddOn
    {
        public int id { get; set; }
        public int quantity { get; set; }
    }
    public class PastryMaterialAddOn
    {
        public int id { get; set; }
        public int quantity { get; set; }
    }
    public class AddNewAddOnRequest
    {
        public string AddOnName { get; set; }
        public int Quantity { get; set; }
    }

    public class employee
    {
        public string name { get; set; }
        public Guid userId { get; set; }
    }
}
