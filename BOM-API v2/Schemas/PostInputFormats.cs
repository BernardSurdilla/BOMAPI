using System.ComponentModel.DataAnnotations;

namespace BillOfMaterialsAPI.Schemas
{
    //MaterialEntryFormat
    public class PostMaterial
    {
        [Required][MaxLength(50)] public string materialName { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
    }
    //MaterialIngredientsEntryFormatWOMaterialId
    public class SubPostMaterialIngredients
    {
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredientType { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
    }
    //MaterialIngredientEntryFormat
    public class PostMaterialIngredient
    {
        //To what material this ingredient is connected to
        [Required][MaxLength(25)] public string materialId { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredientType { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
    }
    //MaterialAndMaterialIngredientsEntryFormat
    public class PostMaterial_MaterialIngredients
    {
        [Required][MaxLength(50)] public string materialName { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }

        [Required] public List<SubPostMaterialIngredients> ingredients { get; set; }
    }

    public class PostPastryMaterial
    {
        [Required] public byte[] designId { get; set; }
        [Required] public string mainVariantName { get; set; }
        [Required] public List<PostIngredients> ingredients { get; set; }
        public PostPastryMaterialOtherCost? otherCost { get; set; }
        public List<PostPastryMaterialIngredientImportance>? ingredientImportance { get; set; }
        public List<PostPastryMaterialAddOns>? addOns { get; set; }
        public List<PostPastryMaterialSubVariant>? subVariants { get; set; }
    }
    public class PostPastryMaterialOtherCost
    {
        public double additionalCost { get; set; }
    }
    public class PostIngredients
    {
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredientType { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
    }
    public class PostPastryMaterialIngredientImportance
    {
        [Required] public string itemId { get; set; }
        [Required][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredientType { get; set; }
        [Required][Range(1, 5, ErrorMessage = "Value of importance must be within 1 - 5 only")] public int importance { get; set; }
    }
    public class PostPastryMaterialAddOns
    {
        [Required] public int addOnsId { get; set; }
        [Required] public double amount { get; set; }
    }
    public class PostPastryMaterialSubVariant
    {
        [Required] public string subVariantName { get; set; }
        [Required] public List<PostPastryMaterialSubVariantIngredients> subVariantIngredients { get; set; }
        public List<PostPastryMaterialSubVariantAddOns>? subVariantAddOns { get; set; }
    }
    public class PostPastryMaterialSubVariantIngredients
    {
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredientType { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
    }
    public class PostPastryMaterialSubVariantAddOns
    {
        [Required] public int addOnsId { get; set; }
        [Required] public double amount { get; set; }
    }

    public class PostDesign
    {
        [MaxLength(50)] public string displayName { get; set; }
        [MaxLength(50)] public string displayPictureUrl { get; set; }
        public string cakeDescription { get; set; }
        public List<Guid>? designTagIds { get; set; }
        public List<string>? designShapeNames { get; set; }
        public List<PostDesignAddOns>? designAddOns { get; set; }
        public byte[]? displayPictureData { get; set; }
    }
    public class PostTags
    {
        public string designTagName { get; set; }
    }
    public class PostDesignTags
    {
        [Required] public List<Guid> designTagIds { get; set; }
    }
    public class PostDesignAddOns
    {
        [Required] public int addOnsId { get; set; }
        [Required][MaxLength(50)] public string addOnName { get; set; }
        [Required] public int quantity { get; set; }
        [Required] public double price { get; set; }
    }
    public class PostDesignShape
    {
        [Required] public Guid designId { get; set; }
        [Required] public string shapeName { get; set; }
    }
}
