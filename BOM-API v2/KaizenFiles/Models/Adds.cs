namespace BOM_API_v2.KaizenFiles.Models
{
    public class Adds
    {
        public class AddOnDetails
        {
            public string name { get; set; }
            public double price { get; set; }
            public double size { get; set; }

        }
        public class UpdateAddOnRequest
        {
            public string name { get; set; }
            public double price { get; set; }
        }

    }
}
