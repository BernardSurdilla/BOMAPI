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
    }   

    public class KaizenTables : DbContext
    {
        public KaizenTables(DbContextOptions<KaizenTables> options) : base(options) { }

        public DbSet<Orders> Orders { get; set; }
        public DbSet<Item> Item { get; set; }
    }
}