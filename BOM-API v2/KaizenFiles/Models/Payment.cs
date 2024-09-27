namespace BOM_API_v2.KaizenFiles.Models
{
    public class SetPayment
    {
        public double amount { get; set; }
        public string option { get; set; }
    }
    public class PaymentRequest
    {
        public string option {  set; get; }
        public double amount { get; set; }
    }   

    public class PaymentRequestResponse
    {
        public PaymentDataWrapper Data { get; set; }
    }

    public class PaymentDataWrapper
    {
        public string id { get; set; }
        public string type { get; set; }
        public PaymentAttributes attributes { get; set; }
    }

    public class PaymentAttributes
    {
        public int amount { get; set; }
        public bool archived { get; set; }
        public string currency { get; set; }
        public string description { get; set; }
        public bool livemode { get; set; }
        public int fee { get; set; }
        public string remarks { get; set; }
        public string status { get; set; }
        public int? tax_amount { get; set; }
        public List<Tax> taxes { get; set; }
        public string checkout_url { get; set; }
        public string reference_number { get; set; }
        public long created_at { get; set; }
        public long updated_at { get; set; }
        public List<PaymentDetail> payments { get; set; }
    }

    public class Tax
    {
        public int Amount { get; set; }
        public string Currency { get; set; }
        public bool Inclusive { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
    }

    public class PaymentDetail
    {
        public PaymentAttributes Data { get; set; }
    }
}
