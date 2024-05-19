using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json.Serialization;

namespace CRUDFI.Models
{
    public class Order
    {

        public Guid Id { get; set; }

        public Guid customerId { get; set; }

        public byte[]? designId { get; set; } = null;

        public Guid? employeeId { get; set; }

        public string orderName { get; set; } = "";

        public bool isActive { get; set; }

        public decimal price { get; set; }

        public string type { get; set; } = "";

        public int quantity { get; set; } //how many orders does he have

        public string status { get; set; } = ""; //ongoing, delayed, cancelled

        public DateTime CreatedAt { get; set; } //when it was ordered

        public string lastUpdatedBy { get; set; } = ""; //confirmed or not

        public DateTime? lastUpdatedAt { get; set; } //when was confirmed or not,, picked up or not
    }
}
