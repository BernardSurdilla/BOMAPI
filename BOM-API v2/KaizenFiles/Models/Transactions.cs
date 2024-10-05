namespace BOM_API_v2.KaizenFiles.Models
{
    public class Transactions
    {
        public class GetTransactions
        {

            public string id { get; set; }
            public string orderId { get; set; }
            public string status { get; set; }
            public double totalPrice { get; set; }
            public double totalPaid { get; set; }
            public DateTime date {  get; set; }

        }
    }
}
