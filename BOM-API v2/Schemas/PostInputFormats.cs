using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillOfMaterialsAPI.Schemas
{
    public class PostIngredients
    {
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    //MaterialEntryFormat
    public class PostMaterial
    {
        [Required][MaxLength(50)] public string material_name { get; set; }
        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    //MaterialIngredientEntryFormat
    public class PostMaterialIngredient
    {
        //To what material this ingredient is connected to
        [Required][MaxLength(25)] public string material_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    //MaterialAndMaterialIngredientsEntryFormat
    public class PostMaterial_MaterialIngredients
    {
        [Required][MaxLength(50)] public string material_name { get; set; }
        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required] public List<SubPostMaterialIngredients> ingredients { get; set; }
    }
    //MaterialIngredientsEntryFormatWOMaterialId
    public class SubPostMaterialIngredients
    {
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
}
