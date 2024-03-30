using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillOfMaterialsAPI.Schemas
{
    [PrimaryKey("ingredient_id")]
    public class Ingredients
    {
        [Required][Key][MaxLength(25)] public string ingredient_id { get; set; }

        //The id of the cake this ingredient is a part of
        [Required][ForeignKey("PastryMaterials")] public string pastry_material_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }

        //What kind of ingredient is this
        //If wether it is from the inventory or a material
        //Dictates where the API should look up the id in the item_id column
        [Required][MaxLength(3)][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }

        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required] public bool isActive { get; set; }
        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }

        public PastryMaterials PastryMaterials { get; set; }

    }
    public static class IngredientType
    {
        public const string Material = "MAT";
        public const string InventoryItem = "INV";
    }

    //Table for the components of a cake (or any pastry)
    [PrimaryKey("pastry_material_id")]
    public class PastryMaterials
    {
        [Required][ForeignKey("Designs.DesignId")][MaxLength(16)] public string DesignId;
        [Required][Key][MaxLength(26)] public string pastry_material_id { get; set; }

        [Required] public bool isActive { get; set; }
        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }
    }
    public class SubPastryMaterials_materials_column
    {
        [Required][MaxLength(25)] public string mat_ing_id { get; set; }
    }

    //Pastry ingredients that is made from a combination of 2 or more items
    //Icing, batter?, etc.
    //ANYTHING MADE WITH A COMBINATION OF ITEMS
    [PrimaryKey("material_id")]
    public class Materials
    {
        [Key][Required][MaxLength(25)] public string material_id { get; set; }
        [Required][MaxLength(50)] public string material_name { get; set; }

        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
        
        [Required] public bool isActive { get; set; }
        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }
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

        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required] public bool isActive { get; set; }
        [Required] public DateTime dateAdded { get; set; }
        [Required] public DateTime lastModifiedDate { get; set; }

        public Materials Materials { get; set; }
    }

    //ACCOUNT
    /*
    public class Users : IdentityUser
    {
        [Required][MaxLength(50)] public string DisplayName { get; set; }
        [Required] public DateTime JoinDate { get; set; }

    }
    */
}
