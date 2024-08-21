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
        /*
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IngredientSubtractionHistory>().OwnsOne(x => x.item_subtraction_info, ownedNavigationBuilder =>
            {
                ownedNavigationBuilder.ToJson();
            });
        }
        */


        public DbSet<PastryMaterials> PastryMaterials { get; set; }
        public DbSet<Ingredients> Ingredients { get; set; }
        public DbSet<PastryMaterialAddOns> PastryMaterialAddOns { get; set; }

        public DbSet<PastryMaterialSubVariants> PastryMaterialSubVariants { get; set; }
        public DbSet<PastryMaterialSubVariantIngredients> PastryMaterialSubVariantIngredients { get; set; }
        public DbSet<PastryMaterialSubVariantAddOns> PastryMaterialSubVariantAddOns { get; set; }

        public DbSet<IngredientSubtractionHistory> IngredientSubtractionHistory { get; set; }
        public DbSet<OrderIngredientSubtractionLog> OrderIngredientSubtractionLog { get; set; }
        public DbSet<PastryMaterialIngredientImportance> PastryMaterialIngredientImportance { get; set; }

        public DbSet<Materials> Materials { get; set; }
        public DbSet<MaterialIngredients> MaterialIngredients { get; set; }

        public DbSet<Designs> Designs { get; set; }
        public DbSet<DesignTags> DesignTags { get; set; }
        public DbSet<DesignTagsForCakes> DesignTagsForCakes { get; set; }
        public DbSet<DesignImage> DesignImage { get; set; }
    }   

    public class KaizenTables : DbContext
    {
        public KaizenTables(DbContextOptions<KaizenTables> options) : base(options) { }

        public DbSet<Orders> Orders { get; set; }
        public DbSet<Item> Item { get; set; }
        public DbSet<AddOns> AddOns { get; set; }
        public DbSet<DesignAddOns> DesignAddOns { get; set; }
        public DbSet<SubOrder> SubOrder { get; set; }
        public DbSet<OrderAddon> OrderAddon { get; set; }
        public DbSet<Sale> Sale { get; set; }
        public DbSet<ThresholdConfig> ThresholdConfig { get; set; }
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