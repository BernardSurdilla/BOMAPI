namespace CRUDFI.Models
{
    public class Ingri
    {
        public int Id { get; set; }

        public string name { get; set; } = "";


        public double quantity { get; set; }

        public string measurements { get; set; } = "";


        public decimal price { get; set; }

        public bool isActive { get; set; }


        public string status { get; set; } = "";

        public string type { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public string username { get; set; } = "";

        public string? lastUpdatedBy { get; set; } 

        public DateTime lastUpdatedAt { get; set; }
    }
    public class ItemUpdateRequest
    {
        public string name { get; set; } = "";
        public int QuantityToSubtract { get; set; }
    }
    public class IngriDTO
    {

        public string name { get; set; } = "";

        public double quantity { get; set; }

        public string measurements { get; set; } = "";

        public decimal price { get; set; }

        public string type { get; set; } = "";

    }

    public class IngriDTP
    {
        public int Id { get; set; }

        public string name { get; set; } = "";

        public double quantity { get; set; }

        public string measurements { get; set; } = "";

        public decimal price { get; set; }

        public string status { get; set; } = "";

        public string type { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public string? lastUpdatedBy { get; set; }

        public DateTime lastUpdatedAt { get; set; }

        public bool isActive { get; set; }
    }
}
