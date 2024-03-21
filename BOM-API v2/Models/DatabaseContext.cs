using BillOfMaterialsAPI.Schemas;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace BillOfMaterialsAPI.Models
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        {

        }
        public DbSet<Ingredients> Ingredients { get; set; }
        public DbSet<Materials> Materials { get; set; }
        public DbSet<MaterialIngredients> MaterialIngredients { get; set; }
        public DbSet<PastryMaterials> PastryMaterials { get; set; }
    }

    public class AccountDatabaseContext : IdentityDbContext<Users>
    {
        public AccountDatabaseContext(DbContextOptions<AccountDatabaseContext> options) :
            base(options)
        { }
    }
    public class LoggingDatabaseContext : DbContext
    {
        public LoggingDatabaseContext(DbContextOptions<LoggingDatabaseContext> options): base(options) { }
        public DbSet<TransactionLogs> TransactionLogs {  get; set; }

    }
}
