using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json.Serialization;

namespace CRUDFI.Models
{
    public class Order
    {

        public Guid Id { get; set; }

        public Guid? customerId { get; set; }

        public string customerName { get; set; } = string.Empty;

        public string employeeName { get; set; } = string.Empty;

        public string designName {  get; set; } = string.Empty;

        public byte[]? designId { get; set; } = null;

        public Guid? employeeId { get; set; }

        public string orderName { get; set; } = "";

        public bool isActive { get; set; }

        public double price { get; set; }

        public string type { get; set; } = "";

        public int quantity { get; set; } //how many orders does he have

        public string status { get; set; } = ""; //ongoing, delayed, cancelled

        public DateTime CreatedAt { get; set; } //when it was ordered

        public string lastUpdatedBy { get; set; } = ""; //confirmed or not

        public DateTime? lastUpdatedAt { get; set; } //when was confirmed or not,, picked up or not

        public DateTime? PickupDateTime { get; set; }

        public string Description { get; set; }

        public string size { get; set; }

        public string flavor { get; set; }
    }
    public class OrderDTO
    {
        public string OrderName { get; set; } = "";
        public string customerName { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Quantity { get; set; }
    }
    public class FinalOrder
    {
        public string OrderName { get; set; } = "";
        public string designName { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Quantity { get; set; }
        public string Description { get; set; }
        public string size { get; set; }
        public string flavor { get; set; }
        public string type { get; set; } = "";
        public DateTime? PickupDateTime { get; set; }

        public List<AddOnDetails2> AddOns { get; set; } = new List<AddOnDetails2>(); // List of add-ons
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
    public class DesignAddOnsUpdateDTO
    {
        public List<AddOnDTO> AddOnsToAdd { get; set; }
        public List<string> AddOnsToRemove { get; set; }
    }

    public class AddOnDTO
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
    }

    public class DesignAddOnsDTO
    {
        public List<AddOnDTOS> AddOns { get; set; }
    }

    public class AddOnDTOS
    {
        public int AddOnId { get; set; }
        public string AddOnName { get; set; }
        public double PricePerUnit { get; set; }
        public int Quantity { get; set; }
    }
    public class AddOnDPOS
    {
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
        public string AddOnName { get; set; }
        public double PricePerUnit { get; set; }
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
    public class AddOnDetails
    {
        public string name { get; set; }
        public double pricePerUnit { get; set; }
        public int quantity { get; set; }
        public double size { get; set; }

    }
    public class AddOnDetails2
    {
        public string name { get; set; }
        public double pricePerUnit { get; set; }
        public int quantity { get; set; }
        public double total { get; set; }

    }
    public class ManageAddOnsRequest
    {
        public List<ManageAddOnAction> Actions { get; set; }
    }

    public class ManageAddOnAction
    {
        public string ActionType { get; set; } // 'setquantity' or other actions
        public string AddOnName { get; set; } // Not needed if addOnName is passed as a query parameter
        public int Quantity { get; set; }
    }

    public class OrderAddOn
    {
        public int AddOnId { get; set; }
        public string AddOnName { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
    }

    public class DesignAddOn
    {
        public int DesignAddOnId { get; set; }
        public string AddOnName { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
    }
    public class AddNewAddOnRequest
    {
        public string AddOnName { get; set; }
        public int Quantity { get; set; }
    }
    public class employee
    {
        public string name { get; set; }
    }
}
