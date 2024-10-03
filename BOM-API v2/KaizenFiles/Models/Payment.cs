namespace BOM_API_v2.KaizenFiles.Models
{
    public class GetRequest
    {
        public string reference { get; set; }
    }
    public class GetResponse
    {
        public bool has_more { get; set; } // Indicates if there are more pages of results
        public List<GetData> data { get; set; } // A list of data items
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
        public bool archived { get; set; } // Indicates if the link is archived
        public string currency { get; set; } // The currency of the payment
        public string description { get; set; } // Description of the payment
        public bool livemode { get; set; } // Indicates if the link is in live mode
        public int fee { get; set; } // Fee associated with the payment
        public string remarks { get; set; } // Any remarks (can be null)
        public string status { get; set; } // Status of the payment link
        public int? tax_amount { get; set; } // Tax amount (can be null)
        public string checkout_url { get; set; } // URL for checkout
        public string reference_number { get; set; } // Reference number for the payment
        public long created_at { get; set; } // Timestamp of creation
        public long updated_at { get; set; } // Timestamp of last update
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
        public string access_url { get; set; } // Access URL for the payment
        public int amount { get; set; } // Amount paid
        public string balance_transaction_id { get; set; } // Transaction ID for balance
        public Billing billing { get; set; } // Billing information
        public string currency { get; set; } // Currency of the payment
        public string description { get; set; } // Description of the payment
        public bool disputed { get; set; } // Indicates if the payment is disputed
        public string external_reference_number { get; set; } // External reference number
        public int fee { get; set; } // Fee for the payment
        public bool? instant_settlement { get; set; } // Instant settlement status (can be null)
        public bool livemode { get; set; } // Indicates if in live mode
        public int net_amount { get; set; } // Net amount after fees
        public string origin { get; set; } // Origin of the payment
        public string payment_intent_id { get; set; } // Payment intent ID (can be null)
        public string payout { get; set; } // Payout information (can be null)
        public PaymentSource source { get; set; } // Source of the payment
        public string statement_descriptor { get; set; } // Descriptor for statement
        public string status { get; set; } // Status of the payment
        public int? tax_amount { get; set; } // Tax amount (can be null)
        public long available_at { get; set; } // When the payment is available
        public long created_at { get; set; } // Timestamp of creation
        public long credited_at { get; set; } // Timestamp of crediting
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



    public class SourceResponse
    {
        public SourceData data { get; set; }
    }

    public class SourceData
    {
        public string id { get; set; }
        public string type { get; set; }
        public SourceAttributes attributes { get; set; }
    }

    public class SourceAttributes
    {
        public int amount { get; set; }
        public string currency { get; set; }
        public ResponseRedirect redirect { get; set; }
        public string status { get; set; }
        public bool livemode { get; set; }
        public int created_at { get; set; }
        public int updated_at { get; set; }
    }

    public class ResponseRedirect
    {
        public string checkout_url { get; set; }
        public string success { get; set; }
        public string failed { get; set; }
    }



    public class PayMongoWebhookEvent
    {
        public string id { get; set; }
        public string type { get; set; }
        public WebhookAttributes attributes { get; set; }
    }

    public class WebhookAttributes
    {
        public int amount { get; set; }
        public bool archived { get; set; }
        public string currency { get; set; }
        public string description { get; set; }
        public bool livemode { get; set; }
        public int fee { get; set; }
        public string remarks { get; set; }
        public string status { get; set; }
        public int tax_amount { get; set; }
    }


    public class PaymentRequest
    {
        public string option {  set; get; }
    }   

    public class PaymentRequestResponse
    {
        public PaymentDataWrapper Data { get; set; }
    }

    public class PaymentDataWrapper
    {
        public string id { get; set; }
        public PaymentAttributes attributes { get; set; }
    }

    public class PaymentAttributes
    {
        public int amount { get; set; }
        public string status { get; set; }
        public string checkout_url { get; set; }
        public string reference_number { get; set; }
    }
}
