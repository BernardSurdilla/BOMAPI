namespace BOM_API_v2.KaizenFiles.Models
{
    public class Notif
    {
        public Guid? notifId { get; set; }
        public Guid? userId { get; set; }
        public DateTime dateCreated { get; set; }
        public string message { get; set; }
        public bool isRead { get; set; }
    }
}

