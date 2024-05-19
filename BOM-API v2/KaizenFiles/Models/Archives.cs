namespace CRUDFI.Models
{
    public class Archives
    {
        public Guid Id { get; set; }

        public string orderName { get; set; } = "";

        public decimal price { get; set; }

        public DateTime CreatedAt { get; set; }

        public string lastUpdatedBy { get; set; }


    }
}
