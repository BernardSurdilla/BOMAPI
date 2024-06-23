using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillOfMaterialsAPI.Schemas
{
    //MaterialEntryFormat
    public class PostMaterial
    {
        [Required][MaxLength(50)] public string material_name { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    //MaterialIngredientsEntryFormatWOMaterialId
    public class SubPostMaterialIngredients
    {
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    //MaterialIngredientEntryFormat
    public class PostMaterialIngredient
    {
        //To what material this ingredient is connected to
        [Required][MaxLength(25)] public string material_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    //MaterialAndMaterialIngredientsEntryFormat
    public class PostMaterial_MaterialIngredients
    {
        [Required][MaxLength(50)] public string material_name { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required] public List<SubPostMaterialIngredients> ingredients { get; set; }
    }

    public class PostPastryMaterial
    {
        [Required] public byte[] design_id { get; set; }
        [Required] public string main_variant_name { get; set; }
        [Required] public List<PostIngredients> ingredients { get; set; }
        public List<PostPastryMaterialSubVariant>? sub_variants { get; set; }
    }
    public class PostIngredients
    {
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    public class PostPastryMaterialSubVariant
    {
        [Required] public string sub_variant_name { get; set; }
        [Required] public List<PostPastryMaterialSubVariantIngredients> sub_variant_ingredients { get; set; }
    }
    public class PostPastryMaterialSubVariantIngredients
    {
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }

    public class PostDesign
    {
        [MaxLength(50)] public string display_name { get; set; }
        [MaxLength(50)] public string display_picture_url { get; set; }
        public string cake_description { get; set; }
        public List<Guid>? design_tag_ids { get; set; }
        public byte[]? display_picture_data { get; set; }
    }
    public class PostTags
    {
        public string design_tag_name { get; set; }
    }
    public class PostDesignTags
    {
        [Required] public List<Guid> design_tag_ids { get; set; }
    }
}
