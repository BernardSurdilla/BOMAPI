using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json.Serialization;

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

        public byte[]? designId { get; set; } = null;

        public Guid? employeeId { get; set; }

        public bool isActive { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public double price { get; set; }

        public string type { get; set; } = "";

        public int quantity { get; set; } //how many orders does he have

        public string status { get; set; } = ""; //ongoing, delayed, cancelled

        public DateTime CreatedAt { get; set; } //when it was ordered

        public string? lastUpdatedBy { get; set; } = ""; //confirmed or not

        public DateTime? lastUpdatedAt { get; set; } //when was confirmed or not,, picked up or not

        public string Description { get; set; }

        public string size { get; set; }

        public string flavor { get; set; }
    }
    public class Custom
    {
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public int quantity { get; set; }
        public string customerName { get; set; } = string.Empty;
        public string cover { get; set; } = "";
        public string Description { get; set; }
        public string size { get; set; }
        public string flavor { get; set; }
        public string picture { get; set; }
        public string PickupDate { get; set; } = "yyyy-mm-dd";
        public string PickupTime { get; set; } = "hh:mm AM/PM";
        public string message { get; set; }
        public string type { get; set; }
        public string description { get; set; }
    }
    public class PostCustomOrder
    {
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public int quantity { get; set; }
        public string cover { get; set; } = "";
        public string Description { get; set; }
        public string size { get; set; }
        public string flavor { get; set; }
        public string picture { get; set; }
        public string message {  get; set; }
        public string type { get; set; }
        public string PickupDate { get; set; } = "yyyy-mm-dd";
        public string PickupTime { get; set; } = "hh:mm AM/PM";
    }
    public class CustomOrderUpdateRequest
    {
        public string DesignName { get; set; }
        public decimal Price { get; set; }
    }

    public class CustomPay
    {
        public string type { get; set; } = "";
        public DateTime? PickupDateTime { get; set; }
        public string payment { get; set; } = "";
    }

    public class CustomPartial
    {
        public Guid customId { get; set; }
        public Guid? orderId { get; set; }
        public Guid? designId { get; set; } = null;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? designName { get; set; }
        public double? Price { get; set; }
        public int Quantity { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public string cover { get; set; } = "";
        public string Description { get; set; }
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
        public Guid? designId { get; set; } = null;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? designName { get; set; }
        public double? Price { get; set; }
        public int Quantity { get; set; }
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
        public Guid? employeeId { get; set; }
        public string employeeName { get; set; } = string.Empty;
        public string payment { get; set; } = "";
        public DateTime? PickupDateTime { get; set; }
    }
    public class UpdateOrderDetailsRequest
    {
        public string Description { get; set; }
        public int Quantity { get; set; }
        public string Size { get; set; }
        public string Flavor { get; set; }
        public string color { get; set; }
        public string shape { get; set; }
    }
    public class AdminInitial
    {
        public Guid? Id { get; set; }
        public byte[]? DesignId { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Payment { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public string DesignName { get; set; }
        public DateTime? Pickup { get; set; }
        public string lastUpdatedBy { get; set; } = "";
        public DateTime? lastUpdatedAt { get; set; }
        public bool isActive { get; set; }
    }

    public class toPayInitial
    {
        public Guid suborderId { get; set; }
        public Guid? Id { get; set; }
        public byte[]? designId { get; set; } = null;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string pastryId { get; set; }
        public string DesignName { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public string lastUpdatedBy { get; set; } = "";
        public DateTime? lastUpdatedAt { get; set; }
        public bool isActive { get; set; }
    }
    public class Full
    {
        public Guid suborderId { get; set; }
        public Guid? orderId { get; set; }
        public byte[]? designId { get; set; } = null;
        public Guid CustomerId { get; set; }
        public Guid? employeeId { get; set; }
        public string employeeName { get; set; } = string.Empty;
        public string CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string pastryId { get; set; }
        public string Status { get; set; }
        public string DesignName { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Quantity { get; set; }
        public string Description { get; set; }
        public string Flavor { get; set; }
        public string Size { get; set; }
        public string payment { get; set; } = "";
        public DateTime? PickupDateTime { get; set; }
        public string lastUpdatedBy { get; set; } = "";
        public DateTime? lastUpdatedAt { get; set; }
        public bool isActive { get; set; }
    }
    public class OrderDetails
    {
        public Guid? orderId { get; set; }
        public string Status { get; set; }
        public string payment { get; set; } = "";
        public string type { get; set; } = "";
        public DateTime? PickupDateTime { get; set; }

    }

    public class Cart
    {
        public Guid suborderId { get; set; }
        public Guid? Id { get; set; }
        public byte[]? designId { get; set; } = null;
        public Guid CustomerId { get; set; }
        public Guid? employeeId { get; set; }
        public string employeeName { get; set; } = string.Empty;
        public string CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string pastryId { get; set; }
        public string Status { get; set; }
        public string DesignName { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Quantity { get; set; }
        public string Description { get; set; }
        public string Flavor { get; set; }
        public string Size { get; set; }
        public string lastUpdatedBy { get; set; } = "";
        public DateTime? lastUpdatedAt { get; set; }
        public bool isActive { get; set; }
    }

    public class OrderSummary
    {
        public Guid suborderId { get; set; }
        public Guid? Id { get; set; }
        public string PastryMaterialId { get; set; }
        public byte[]? designId { get; set; } = null;
        public Guid CustomerId { get; set; }
        public Guid? employeeId { get; set; }
        public string employeeName { get; set; } = string.Empty;
        public string CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Payment { get; set; }
        public string pastryId { get; set; }
        public string Status { get; set; }
        public string DesignName { get; set; }
        public string color { get; set; } = string.Empty;
        public string shape { get; set; } = string.Empty;
        public string tier { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Quantity { get; set; }
        public string Description { get; set; }
        public string Flavor { get; set; }
        public string Size { get; set; }
        public string lastUpdatedBy { get; set; } = "";
        public DateTime? lastUpdatedAt { get; set; }
        public bool isActive { get; set; }
    }
    public class CheckOutDetails
    {
        public Guid OrderId { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public string OrderType { get; set; }
        public DateTime? PickupDateTime { get; set; }
        public double OrderTotal { get; set; }

        // List to hold all suborders
        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    public class OrderItem
    {
        public Guid SuborderId { get; set; }
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public string PastryId { get; set; }
        public string Color { get; set; }
        public string Shape { get; set; }
        public string Tier { get; set; }
        public byte[] DesignId { get; set; }
        public string DesignName { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public string LastUpdatedBy { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public string Description { get; set; }
        public string Flavor { get; set; }
        public string Size { get; set; }
        public string CustomerName { get; set; }
        public double SubOrderTotal {  get; set; }

        // List to hold all add-ons for the order item
        public List<OrderAddon1> OrderAddons { get; set; } = new List<OrderAddon1>();
    }
    public class AssignEmp
    {
        public string employeeId { get; set; }
    }

    public class OrderAddon1
    {
        public int AddonId { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        public double AddOnTotal { get; set; }
    }

    public class BuyNow
    {
        public string Type { get; set; }
        public string PickupDate { get; set; } = "yyyy-mm-dd";
        public string PickupTime { get; set; } = "hh:mm AM/PM";
        public string Payment { get; set; }
        public List<OrderDTO> orderItem { get; set; } = new List<OrderDTO>();
    }

    public class OrderDTO
    {
        public int Quantity { get; set; }
        public byte[] DesignId { get; set; }
        public string Description { get; set; }
        public string Flavor { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
    }

    public class CheckOutRequest
    {
        public string Type { get; set; }
        public string PickupDate { get; set; } = "yyyy-mm-dd";
        public string PickupTime { get; set; } = "hh:mm AM/PM";
        public string Payment { get; set; }
        public List<Guid> SuborderIds { get; set; }
    }


    public class SuborderResponse
    {
        public string suborderId { get; set; }
        public string pastryId { get; set; }
        public List<string> addonId {  get; set; }
    }


    public class FinalOrder
    {
        public string OrderId { get; set; }
        public string PastryMaterialId { get; set; }
        public string variantId { get; set; }
        public Guid? customerId { get; set; }
        public string CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string status { get; set; } = "";
        public string payment { get; set; } = "";
        public string lastUpdatedBy { get; set; } = "";
        public DateTime? lastUpdatedAt { get; set; }
        public DateTime? PickupDateTime { get; set; }
        public string Type { get; set; } // Type of the suborder
        public bool IsActive { get; set; } // Whether the suborder is active
        public List<OrderSummary> summary { get; set; } = new List<OrderSummary>();
        public List<AddOnDetails2> AddOns { get; set; } = new List<AddOnDetails2>();
        public List<CustomAddons> customAddons { get; set; } = new List<CustomAddons>();
        public double allTotal { get; set; }
    }
    public class AddOnDetails2
    {
        public string name { get; set; }
        public double pricePerUnit { get; set; }
        public int quantity { get; set; }
        public double total { get; set; }

    }
    public class CustomAddons
    {
        public string? name { get; set; }
        public int? quantity { get; set; }
        public double? price { get; set; }
    }
    public class TotalOrders
    {
        public int Total { get; set; }
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


    public class orderAddons
    {
        public string pastryId { get; set; }
        public List<AddOnDPOS> addOnDPOs { get; set; }
    }

    public class AddOnDPOS
    {
        public int AddOnId { get; set; }
        public string AddOnName { get; set; }
        public double PricePerUnit { get; set; }
        public int Quantity { get; set; }
    }


    public class AddOnDSOS
    {
        public string AddOnName { get; set; }
        public double PricePerUnit { get; set; }
        public int AddOnId { get; set; }
    }

    public class AddOnDS2
    {
        public int addOnsId { get; set; }
        public string AddOnName { get; set; }
        public string? Measurement { get; set; }
        public double PricePerUnit { get; set; }
        public double? size { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime? LastModifiedDate { get; set; }
    }

    public class AddOns
    {
        public int addOnsId { get; set; }
        public string name { get; set; }
        public double pricePerUnit { get; set; }
        public int quantity { get; set; }
        public double size { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime? LastModifiedDate { get; set; }
        public bool IsActive { get; set; }
    }
    public class ManageAddOnAction
    {
        public string ActionType { get; set; } = "";
        public int Quantity { get; set; }
    }
    public class AddOn
    {
        public int addonId { get; set; }
        public int quantity { get; set; }
    }

    public class ManageAddOnQuantityWrapper
    {
        public List<AddOn> manage { get; set; }
    }
    public class PastryMaterialAddOn
    {
        public int AddOnId { get; set; }
        public int Quantity { get; set; }
    }
    public class AddNewAddOnRequest
    {
        public string AddOnName { get; set; }
        public int Quantity { get; set; }
    }

    public class employee
    {
        public string name { get; set; }
        public Guid userId {  get; set; }
    }
}
