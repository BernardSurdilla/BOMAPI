using System.ComponentModel.DataAnnotations;

namespace CRUDFI.Models
{
    public class Ingri
    {
        public string id { get; set; }

        public string name { get; set; } = "";


        public double quantity { get; set; }

        public string measurements { get; set; } = "";


        public decimal price { get; set; }

        public bool isActive { get; set; }


        public string status { get; set; } = "";

        public string type { get; set; } = "";

        public DateTime createdAt { get; set; }

        public string username { get; set; } = "";

        public string? lastUpdatedBy { get; set; }

        public DateTime lastUpdatedAt { get; set; }
    }
   
    public class IngriDTO
    {
        [Required(ErrorMessage = "Name is required.")]
        public string name { get; set; } = "";
        [Required(ErrorMessage = "Measurements is required.")]
        public string measurements { get; set; } = "";
        [Required(ErrorMessage = "Type is required.")]
        public string type { get; set; } = "";
        [Required(ErrorMessage = "Good threshold is required.")]
        public int good { get; set; }
        [Required(ErrorMessage = "Bad threshold is required.")]
        public int bad { get; set; }

    }
    public class IngriDTOs
    {

        public string? name { get; set; } = "";
        public double? quantity { get; set; }
        public string? measurements { get; set; } = "";
        public decimal? price { get; set; }
        public string? type { get; set; } = "";

    }

    public class thresholdUpdate
    {
        public int good { get; set; }
        public int critical { get; set; }
    }

    public class IngriDTP
    {
        public string id { get; set; }

        public string name { get; set; } = "";

        public double quantity { get; set; }

        public string measurements { get; set; } = "";

        public decimal price { get; set; }

        public string status { get; set; } = "";

        public string type { get; set; } = "";

        public DateTime createdAt { get; set; }

        public string? lastUpdatedBy { get; set; }

        public DateTime lastUpdatedAt { get; set; }

        public bool isActive { get; set; }

        public int goodThreshold { get; set; }
        public int criticalThreshold { get; set; }
    }
    public class BatchRequest
    {
        public double price { get; set; }
        public double quantity { get; set; }
        public string expiration { get; set; }
        public string lotNumber {  get; set; }
    }
    public class BatchDto
    {
        public string id { get; set; }
        public string itemId { get; set; }
        public decimal price { get; set; }
        public double quantity { get; set; }
        public DateTime created { get; set; }
        public DateTime? lastModified { get; set; }
        public string? lastModifiedBy { get; set; }
        public bool isActive { get; set; }
        public DateTime? expiration { get; set; }
        public string? lotNumber { get; set; }
    }

}
