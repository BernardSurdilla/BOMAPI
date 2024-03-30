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
}