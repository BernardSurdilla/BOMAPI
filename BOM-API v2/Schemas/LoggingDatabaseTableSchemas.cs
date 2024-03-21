using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
namespace BillOfMaterialsAPI.Schemas
{
    [PrimaryKey("logId")]
    public class TransactionLogs
    {
        [Required][Key][MaxLength(25)] public string logId {  get; set; }
        [Required] public string accountId { get; set; }
        [Required] public string accountName { get; set; }
        [Required] public string accountEmail { get; set; }
        [Required][MaxLength(100)] public string transactionType { get; set; }
        [Required] public DateTime date { get; set; }

        public TransactionLogs(string logId, string accountId, string accountName, string accountEmail, string transactionType, DateTime date)
        {
            this.logId = logId;
            this.accountId = accountId;
            this.accountName = accountName;
            this.accountEmail = accountEmail;
            this.transactionType = transactionType;
            this.date = date;
        }
    }
}
