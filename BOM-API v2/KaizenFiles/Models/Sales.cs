namespace BOM_API_v2.KaizenFiles.Models
{
    public class Sales
    {
        public int id { get; set; }
        public string name { get; set; } // order name
        public string number { get; set; } // contact number
        public string email { get; set; }
        public double price { get; set; }
        public int total { get; set; }
        public DateTime date { get; set; }
    }
    public class SalesSum
    {
        public string name { get; set; }
        public int total { get; set; }
    }

    public class Totals
    {
        public int total { get; set; }
    }
    public class SalesResponse //for daily and weekly
    {
        public string day { get; set; }
        public decimal totalSales { get; set; }
    }
    public class MonthSalesResponse
    {
        public int day { get; set; }
        public decimal totalSales { get; set; }
    }

    public class YearSalesResponse
    {
        public string month { get; set; }
        public decimal totalSales { get; set; }
    }



}
