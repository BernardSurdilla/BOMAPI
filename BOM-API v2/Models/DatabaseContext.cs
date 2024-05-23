using BillOfMaterialsAPI.Schemas;
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

        public DbSet<Designs> Designs { get; set; }
        public DbSet<DesignTags> DesignTags { get; set; }
        public DbSet<DesignTagsForCake> DesignTagsForCakes { get; set; }
        public DbSet<DesignImage> DesignImage { get; set; }
    }   

    public class KaizenTables : DbContext
    {
        public KaizenTables(DbContextOptions<KaizenTables> options) : base(options) { }

        public DbSet<Orders> Orders { get; set; }
        public DbSet<Item> Item { get; set; }
    }

    public class InventoryAccounts : DbContext
    {
        public InventoryAccounts(DbContextOptions<InventoryAccounts> options) : base(options)
        {

        }
        public DbSet<Users> Users { get; set; }
        public DbSet<Employee> Employee { get; set; }
        public DbSet<Customers> Customers { get; set; }
    }
}