using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;
using System;
using MimeKit.Encodings;

namespace BillOfMaterialsAPI.Schemas
{
    
    public static class IngredientType
    {
        public const string Material = "MAT";
        public const string InventoryItem = "INV";
    }

    //Table for the components of a cake (or any pastry)
    [PrimaryKey("pastry_material_id")]
    public class PastryMaterials
    {
        [Required][ForeignKey("Designs")][MaxLength(16)] public byte[] design_id;
        [Required][Key][MaxLength(26)] public string pastry_material_id { get; set; }

        [Required] public string main_variant_name { get; set; }

        [Required][Column("is_active")] public bool is_active { get; set; }
        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }

        public Designs Designs { get; set; }
    }
    [PrimaryKey("ingredient_id")]
    public class Ingredients
    {
        [Required][Key][MaxLength(25)] public string ingredient_id { get; set; }
        [Required][ForeignKey("PastryMaterials")] public string pastry_material_id { get; set; }

        //The id of the cake this ingredient is a part of
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }

        //What kind of ingredient is this
        //If wether it is from the inventory or a material
        //Dictates where the API should look up the id in the item_id column
        [Required][MaxLength(3)][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required][Column("is_active")] public bool is_active { get; set; }
        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }

        public PastryMaterials PastryMaterials { get; set; }

    }
    [PrimaryKey("pastry_material_add_on_id")]
    public class PastryMaterialAddOns
    {
        [Required][Key][MaxLength(29)] public string pastry_material_add_on_id { get; set; }
        [Required][ForeignKey("PastryMaterials")] public string pastry_material_id { get; set; }
        [Required] public int add_ons_id { get; set; }
        [Required] public double amount { get; set; }
        [Required][Column("is_active")] public bool is_active { get; set; }
        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }

        public PastryMaterials PastryMaterials { get; set; }
    }


    [PrimaryKey("pastry_material_sub_variant_id")]
    public class PastryMaterialSubVariants
    {
        [Required][Key][MaxLength(26)] public string pastry_material_sub_variant_id { get; set; }
        [Required][ForeignKey("PastryMaterials")] public string pastry_material_id { get; set; }

        [Required] public string sub_variant_name { get; set; }

        [Required][Column("is_active")] public bool is_active { get; set; }
        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }

        public PastryMaterials PastryMaterials { get; set; }
    }
    [PrimaryKey("pastry_material_sub_variant_ingredient_id")]
    public class PastryMaterialSubVariantIngredients
    {
        [Required][Key][MaxLength(25)] public string pastry_material_sub_variant_ingredient_id { get; set; }

        [Required][ForeignKey("PastryMaterialSubVariants")] public string pastry_material_sub_variant_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }

        //What kind of ingredient is this
        //If wether it is from the inventory or a material
        //Dictates where the API should look up the id in the item_id column
        [Required][MaxLength(3)][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required][Column("is_active")] public bool is_active { get; set; }
        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }

        public PastryMaterialSubVariants PastryMaterialSubVariants { get; set; }
    }
    [PrimaryKey("pastry_material_sub_variant_add_on_id")]
    public class PastryMaterialSubVariantAddOns
    {
        [Required][Key][MaxLength(26)] public string pastry_material_sub_variant_add_on_id { get; set; }
        [Required][ForeignKey("PastryMaterialSubVariants")] public string pastry_material_sub_variant_id { get; set; }
        [Required] public int add_ons_id { get; set; }
        [Required] public double amount { get; set; }
        [Required][Column("is_active")] public bool is_active { get; set; }
        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }

        public PastryMaterialSubVariants PastryMaterialSubVariants { get; set; }
    }

    [PrimaryKey("order_ingredient_subtraction_log_id")]
    public class OrderIngredientSubtractionLog
    {
        [Required][Key] public Guid order_ingredient_subtraction_log_id { get; set; }
        [Required][Column(TypeName = "binary(16)")] public byte[] order_id { get; set; }
        [Required][ForeignKey("IngredientSubtractionHistory")] public Guid ingredient_subtraction_history_id { get; set; }

        public IngredientSubtractionHistory IngredientSubtractionHistory { get; set; }
    }
    [PrimaryKey("ingredient_subtraction_history_id")]
    public class IngredientSubtractionHistory
    {
        [Required][Key] public Guid ingredient_subtraction_history_id { get; set; }
        [Required][Column(TypeName = "json")] public List<ItemSubtractionInfo> item_subtraction_info { get; set; }
        [Required] public DateTime date_subtracted { get; set; }
    }
    public class ItemSubtractionInfo
    {
        public string item_id { get; set; }
        public string item_name { get; set; }

        public string amount_quantity_type;
        public string amount_unit;
        public double amount;
    }
    [PrimaryKey("pastry_material_ingredient_importance_id")]
    public class PastryMaterialIngredientImportance
    {
        [Required][Key] public Guid pastry_material_ingredient_importance_id { get; set; }
        [Required][ForeignKey("PastryMaterials")] public string pastry_material_id { get; set; }

        [Required] public string item_id { get; set; }
        [Required] public string ingredient_type { get; set; }
        [Required] public int importance { get; set; }

        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }
        [Column("is_active")] public bool is_active { get; set; }

        public PastryMaterials PastryMaterials { get; set; }
    }

    //Pastry ingredients that is made from a combination of 2 or more items
    //Icing, batter?, etc.
    //ANYTHING MADE WITH A COMBINATION OF ITEMS
    [PrimaryKey("material_id")]
    public class Materials
    {
        [Key][Required][MaxLength(25)] public string material_id { get; set; }
        [Required][MaxLength(50)] public string material_name { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
        
        [Required][Column("is_active")] public bool is_active { get; set; }
        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }
    }
    //Table for the items of a 'Materials' table row
    [PrimaryKey("material_ingredient_id")]
    public class MaterialIngredients
    {
        [Required][Key][MaxLength(26)] public string material_ingredient_id { get; set; }
        //To what material this ingredient is connected to
        [Required][ForeignKey("Materials")][MaxLength(25)] public string material_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }

        //What kind of ingredient is this
        //If wether it is from the inventory or a material
        //Dictates where the API should look up the id in the item_id column
        [Required][MaxLength(3)][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required][Column("is_active")] public bool is_active { get; set; }
        [Required] public DateTime date_added { get; set; }
        [Required] public DateTime last_modified_date { get; set; }

        public Materials Materials { get; set; }
    }

    [PrimaryKey("design_id")]
    public class Designs
    {
        [MaxLength(16)][Key] public byte[] design_id { get; set; }
        [MaxLength(50)] public string display_name { get; set; }
        [MaxLength(50)] public string display_picture_url { get; set; }
        public string? cake_description { get; set; }
        public bool is_active { get; set; }
    }
    [PrimaryKey("design_tag_id")]
    public class DesignTags
    {
        [Key] public Guid design_tag_id { get; set; }
        public string design_tag_name { get; set; }
        [Column("is_active")] public bool is_active { get; set; }
    }
    [PrimaryKey("design_tags_for_cake_id")]
    public class DesignTagsForCakes
    {
        [Key]public Guid design_tags_for_cake_id { get; set; }

        [ForeignKey("Designs")]public byte[] design_id { get; set; }
        [ForeignKey("DesignTags")]public Guid design_tag_id { get; set; }

        [Column("is_active")] public bool is_active { get; set; }

        public Designs Designs { get; set; }
        public DesignTags DesignTags { get; set; }
    }
    [PrimaryKey("design_picture_id")]
    public class DesignImage
    {
        [Key] public Guid design_picture_id { get; set; }
        [ForeignKey("Designs")] public byte[] design_id { get; set; }
        public byte[] picture_data { get; set; }
        [Column("is_active")] public bool is_active { get; set; }

        public Designs Designs { get; set; }
    }
    [PrimaryKey("design_shape_id")]
    public class DesignShapes
    {
        [Key] public Guid design_shape_id { get; set; }
        [ForeignKey("Designs")] public byte[] design_id { get; set; }
        public string shape_name { get; set; }
        [Column("is_active")] public bool is_active { get; set; }

        public Designs Designs { get; set; }
    }


    //
    // Orders table: Kaizen
    //
    [PrimaryKey("order_id")]
    [Table("orders")]
    public class Orders
    {
        [Required][Key] public byte[] order_id { get; set; }
        [Required] public Guid customer_id { get; set; }
        [Required] public Guid? employee_id { get; set; }
        public string pastry_id { get; set; }

        public DateTime created_at { get; set; }
        [MaxLength(50)] public string status { get; set; }
        public byte[] design_id { get; set; }
        public double price { get; set; }
        [MaxLength(50)] public string? last_updated_by { get; set; }
        public DateTime? last_updated_at { get; set; }
        [MaxLength(50)] public string type { get; set; }
        public bool? is_active { get; set; }
    }
    [PrimaryKey("id")]
    [Table("Item")]
    public class Item
    {
        [Required][Key] public int id { get; set; }
        [MaxLength(50)] public string item_name { get; set; }
        public double quantity { get; set; }
        public double price { get; set; }
        [MaxLength(50)] public string status { get; set; }
        [MaxLength(20)] public string type { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        [MaxLength(50)] public string? last_updated_by { get; set; }
        public DateTime last_updated_at { get; set; }

        public string measurements { get; set; }
    }
    [PrimaryKey("add_ons_id")]
    [Table("addons")]
    public class AddOns
    {
        [Key] public int add_ons_id { get; set; }
        [MaxLength(50)] public string name { get; set; }
        public double price { get; set; }
        [MaxLength(50)] public string? measurement { get; set; }
        public double? size { get; set; }
        [MaxLength(50)] public string? ingredient_type { get; set; }
        public DateTime date_added { get; set; }
        public DateTime? last_modified_date { get; set; }
    }
    [PrimaryKey("SubOrderId")]
    [Table("suborders")]
    public class SubOrder
    {
        [Key]
        [Column("suborder_id")]
        [MaxLength(25)]
        public string SubOrderId { get; set; }

        [Column("OrderId")]
        public byte[] OrderId { get; set; }

        [Column("PastryId")]
        [MaxLength(50)]
        public string PastryId { get; set; }

        [Column("CustomerId")]
        public byte[]? CustomerId { get; set; }

        [Column("EmployeeId")]
        public byte[]? EmployeeId { get; set; }

        [Column("CustomerName")]
        [MaxLength(50)]
        public string CustomerName { get; set; }

        [Column("EmployeeName")]
        [MaxLength(50)]
        public string EmployeeName { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [Column("Status")]
        [MaxLength(50)]
        public string Status { get; set; }

        [Column("DesignId")]
        public byte[] DesignId { get; set; }

        [Column("DesignName")]
        [MaxLength(50)]
        public string DesignName { get; set; }

        [Column("price")]
        public double Price { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("Size")]
        [MaxLength(50)]
        public string Size { get; set; }

        [Column("Flavor")]
        [MaxLength(50)]
        public string Flavor { get; set; }

        [Column("Description")]
        public string? Description { get; set; }

        [Column("last_updated_by")]
        [MaxLength(50)]
        public string? LastUpdatedBy { get; set; }

        [Column("last_updated_at")]
        public DateTime? LastUpdatedAt { get; set; }

        [Column("type")]
        [MaxLength(50)]
        public string? Type { get; set; }

        [Column("PickupDateTime")]
        public DateTime? PickupDateTime { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }
    [PrimaryKey("OrderAddOnId")]
    [Table("orderaddons")]
    public class OrderAddon
    {
        [Key]
        [Column("orderAddOnId")]
        public int OrderAddOnId { get; set; }

        [Column("OrderId")]
        public Guid OrderId { get; set; } // Assuming OrderId is a binary(16) GUID

        [Column("addOnsId")]
        public int? AddOnsId { get; set; } // Nullable int

        [Column("name")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("price")]
        public double? Price { get; set; } // Nullable double

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("Total")]
        public double Total { get; set; }

        // Navigation properties
        public Orders Order { get; set; }
        public AddOns? AddOn { get; set; }
    }
    [PrimaryKey("Id")] // Assuming your ORM supports this attribute
    [Table("sales")]
    public class Sale
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Name")]
        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        [Column("Contact")]
        [Required]
        [MaxLength(10)]
        public string Contact { get; set; }

        [Column("Email")]
        [Required]
        [MaxLength(50)]
        public string Email { get; set; }

        [Column("Cost")]
        [Required]
        public double Cost { get; set; }

        [Column("Total")]
        [Required]
        public int Total { get; set; }

        [Column("Date")]
        [Required]
        public DateTime Date { get; set; }
    }
    [PrimaryKey("Id")] // Assuming your ORM supports this attribute
    [Table("thresholdconfig")]
    public class ThresholdConfig
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("GoodThreshold")]
        [Required]
        public int GoodThreshold { get; set; }

        [Column("MidThreshold")]
        [Required]
        public int MidThreshold { get; set; }

        [Column("CriticalThreshold")]
        [Required]
        public int CriticalThreshold { get; set; }
    }

    //Legacy account tables
    [Table("users")]
    [PrimaryKey("user_id")]
    public class Users
    {
        [Column("UserId")][Key] public byte[] user_id { get; set; }
        [Column("Type")] public int type { get; set; }
        [Column("Username")] public string user_name { get; set; }
        [Column("Password")] public string password { get; set; }
        [Column("DisplayName")] public string display_name { get; set; }
        [Column("JoinDate")] public DateTime join_date { get; set; }
        [Column("Email")] public string email { get; set; }
        [Column("Contact")] public string contact { get; set; }
    }
    [Table("customers")]
    [PrimaryKey("customer_id")]
    public class Customers
    {
        [Column("CustomerId")][Key] public byte[] customer_id { get; set; }
        [Column("UserId")][ForeignKey("Users")] public byte[] user_id { get; set; }
        [Column("TimesOrdered")] public int times_ordered { get; set; }

        public Users Users { get; set; }
    }
    [Table("employee")]
    [PrimaryKey("employee_id")]
    public class Employee
    {
        [Column("EmployeeId")][Key] public byte[] employee_id { get; set; }
        [Column("UserId")][ForeignKey("Users")] public byte[] user_id { get; set; }
        [Column("EmploymentDate")] public DateTime employment_date { get; set; }

        public Users Users { get; set; }
    }
}
