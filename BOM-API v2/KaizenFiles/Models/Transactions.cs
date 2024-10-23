namespace BOM_API_v2.KaizenFiles.Transactions
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

    public class GetResponses
    {
        public GetData data { get; set; } // Change to a single object, not a list
    }

    public class GetData
    {
        public string id { get; set; } // The unique identifier for the link
        public string type { get; set; } // The type of object, e.g., "link"
        public GetAttributes attributes { get; set; } // The attributes of the link
    }

    public class GetAttributes
    {
        public int amount { get; set; } // The amount of the payment link
        public string status { get; set; } // Status of the payment link
        public int? tax_amount { get; set; } // Tax amount (can be null)
        public string checkout_url { get; set; } // URL for checkout
        public string reference_number { get; set; } // Reference number for the payment
        public List<Payment> payments { get; set; } // List of payment details
    }

    public class Payment
    {
        public PaymentData data { get; set; } // Details of the payment
    }

    public class PaymentData
    {
        public string id { get; set; } // Unique identifier for the payment
        public string type { get; set; } // The type of object, e.g., "payment"
        public PaymentAttributesFull attributes { get; set; } // The attributes of the payment
    }

    public class PaymentAttributesFull
    {
        public Billing billing { get; set; } // Billing information
        public string currency { get; set; } // Currency of the payment
        public int fee { get; set; } // Fee for the payment
        public string origin { get; set; } // Origin of the payment
        public PaymentSource source { get; set; } // Source of the payment
        public string status { get; set; } // Status of the payment
        public int? tax_amount { get; set; } // Tax amount (can be null)
        public long paid_at { get; set; } // Timestamp of when it was paid
        public long updated_at { get; set; } // Timestamp of last update
    }

    public class Billing
    {
        public Address address { get; set; } // Billing address
        public string email { get; set; } // Email of the customer
        public string name { get; set; } // Name of the customer
        public string phone { get; set; } // Phone number of the customer
    }

    public class Address
    {
        public string city { get; set; } // City of the billing address
        public string country { get; set; } // Country of the billing address
        public string line1 { get; set; } // Line 1 of the address
        public string line2 { get; set; } // Line 2 of the address
        public string postal_code { get; set; } // Postal code of the address
        public string state { get; set; } // State of the billing address
    }

    public class PaymentSource
    {
        public string id { get; set; } // ID of the source
        public string type { get; set; } // Type of the source, e.g., "gcash"
    }


}
