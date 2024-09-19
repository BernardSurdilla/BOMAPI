using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json.Serialization;

namespace BOM_API_v2.KaizenFiles.Models
{
    public class Notif
    {
        public Guid? customId { get; set; }
        public string designName { get; set; } = string.Empty;
        public Guid? userId { get; set; }
        public DateTime dateCreated { get; set; }
        public string Message { get; set; }
        public bool is_read { get; set; }
    }
}

