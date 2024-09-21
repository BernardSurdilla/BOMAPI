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
    public class ThresholdConfig
    {
        public int GoodThreshold { get; set; }
        public int MidThreshold { get; set; }
        public int CriticalThreshold { get; set; }
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
        public string good { get; set; }
        public string bad { get; set; }

    }
    public class IngriDTOs
    {

        public string name { get; set; } = "";
        public double quantity { get; set; }
        public string measurements { get; set; } = "";
        public decimal price { get; set; }
        public string type { get; set; } = "";

    }

    public class thresholdUpdate
    {
        public int good { get; set; }
        public int critical { get; set; }
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
