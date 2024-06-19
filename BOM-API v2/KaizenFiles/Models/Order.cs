using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json.Serialization;

namespace CRUDFI.Models
{
    public class Order
    {

        public Guid Id { get; set; } 

        public Guid customerId { get; set; }

        public string customerName { get; set; } = string.Empty;

        public string employeeName { get; set; } = string.Empty;

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
        public double Price { get; set; }
        public int Quantity { get; set; }
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

}
