using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillOfMaterialsAPI.Schemas
{
    public class PatchIngredients
    {
        [Required][MaxLength(25)] public string item_id { get; set; }
        [[Required][MaxLength(3)][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }
        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    public class PatchMaterials
    {
        [Required][MaxLength(50)] public string material_name { get; set; }
        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    public class PatchMaterialIngredients
    {
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    public class PatchPastryMaterials
    {
        [Required][MaxLength(16)] public string DesignId;

    }
}
