using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace LiveChat
{
    public class DirectMessagesDB : DbContext
    {
        public DirectMessagesDB(DbContextOptions<DirectMessagesDB> options) : base(options) { }

        public DbSet<DirectMessages> DirectMessages { get; set; }
    }

    //Live chat table
    [PrimaryKey("direct_message_id")]
    public class DirectMessages
    {
        [Key] public Guid direct_message_id { get; set; }
        public string? sender_account_id { get; set; }
        public string? receiver_account_id { get; set; }
        public string message { get; set; }
        public DateTime date_sent { get; set; }
    }
}
