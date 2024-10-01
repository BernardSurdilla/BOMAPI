using System.ComponentModel.DataAnnotations;

namespace BillOfMaterialsAPI.Schemas
{
    public class PatchIngredients
    {
        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required][MaxLength(3)][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredientType { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
    }
    public class PatchMaterials
    {
        [Required][MaxLength(50)] public string materialName { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
    }
    public class PatchMaterialIngredients
    {
        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
    }
    public class PatchPastryMaterials
    {
        [Required][MaxLength(16)] public byte[] designId { get; set; }
        [Required] public string mainVariantName { get; set; }
    }
    public class PatchPastryMaterialOtherCost
    {
        [Required] public double additionalCost { get; set; }
    }
    public class PatchPastryMaterialIngredientImportance
    {
        [Required] public string itemId { get; set; }
        [Required] public string ingredientType { get; set; }
        [Required] public int importance { get; set; }
    }
    public class PatchPastryMaterialSubVariants
    {
        [Required] public string subVariantName { get; set; }
    }
    public class PatchPastryMaterialAddOn
    {
        [Required] public int addOnsId { get; set; }
        [Required] public double amount { get; set; }
    }
    public class PatchPastryMaterialSubVariantsIngredient
    {
        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required][MaxLength(3)][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredientType { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
    }
    public class PatchPastryMaterialSubVariantAddOn
    {
        [Required] public int addOnsId { get; set; }
        [Required] public double amount { get; set; }
    }
    public class PatchDesignAddOns
    {
        [Required] public int addOnsId { get; set; }
        [Required][MaxLength(50)] public string addOnName { get; set; }
        [Required] public int quantity { get; set; }
        [Required] public double price { get; set; }
    }
    public class PatchDesignShape
    {
        [Required] public Guid designShapeId { get; set; }
        [Required] public string shapeName { get; set; }
    }
}
