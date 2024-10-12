
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BillOfMaterialsAPI.Schemas
{
    public class GetMaterials
    {
        [Required] public SubGetMaterials Material { get; set; }
        [Required] public List<SubGetMaterialIngredients> materialIngredients { get; set; }
        public double costEstimate { get; set; }

        public GetMaterials(SubGetMaterials materials, List<SubGetMaterialIngredients> materialIngredients, double costEst)
        {
            Material = materials;
            this.materialIngredients = materialIngredients;
            costEstimate = costEst;
        }
        public GetMaterials() { }
        public static GetMaterials DefaultResponse()
        {

            GetMaterials response = new GetMaterials(new SubGetMaterials(new Materials()), new List<SubGetMaterialIngredients>(), 0);

            return response;
        }
    }
    public class SubGetMaterials
    {
        [Key][Required][MaxLength(25)] public string materialId { get; set; }
        [Required][MaxLength(50)] public string materialName { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }

        public SubGetMaterials() { }
        public SubGetMaterials(Materials dbRow)
        {
            this.materialId = dbRow.material_id;
            this.materialName = dbRow.material_name;
            this.amount = dbRow.amount;
            this.amountMeasurement = dbRow.amount_measurement;
            this.dateAdded = dbRow.date_added;
            this.lastModifiedDate = dbRow.last_modified_date;
        }
    }
    public class SubGetMaterialIngredients
    {
        [Required][Key] public string materialIngredientId { get; set; }
        //To what material this ingredient is connected to
        [Required][ForeignKey("Materials")][MaxLength(25)] public string materialId { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required] public string itemName { get; set; }

        [Required] public string ingredientType { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }

        public SubGetMaterialIngredients() { }
        public SubGetMaterialIngredients(MaterialIngredients dbRow)
        {
            this.materialIngredientId = dbRow.material_ingredient_id;
            this.materialId = dbRow.material_id;
            this.itemId = dbRow.item_id;
            this.ingredientType = dbRow.ingredient_type;
            this.amount = dbRow.amount;
            this.amountMeasurement = dbRow.amount_measurement;
            this.dateAdded = dbRow.date_added;
            this.lastModifiedDate = dbRow.last_modified_date;
        }
    }

    public class GetIngredients
    {
        [Required] public string ingredientId { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required] public string pastryMaterialId { get; set; }

        [Required] public string ingredientType { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }

        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }

        public GetIngredients() { }
        public GetIngredients(string ingredient_id, string item_id, string pastry_material_id, string ingredient_type, int amount, string amount_measurement, DateTime date_added, DateTime last_modified_date)
        {
            this.ingredientId = ingredient_id;
            this.itemId = item_id;
            this.pastryMaterialId = pastry_material_id;
            this.ingredientType = ingredient_type;
            this.amount = amount;
            this.amountMeasurement = amount_measurement;
            this.dateAdded = date_added;
            this.lastModifiedDate = last_modified_date;
        }
        public static GetIngredients DefaultResponse()
        {
            return new GetIngredients();
        }
    }
    public class GetPastryMaterial
    {
        [Required] public Guid designId { get; set; }
        [Required] public string designName { get; set; }
        [Required][MaxLength(26)] public string pastryMaterialId { get; set; }

        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }

        public double costEstimate { get; set; }
        public double costExactEstimate { get; set; }
        public GetPastryMaterialOtherCost? otherCost { get; set; }

        public string mainVariantName { get; set; }
        public bool ingredientsInStock { get; set; }
        public List<GetPastryMaterialIngredients> ingredients { get; set; }
        public List<GetPastryMaterialIngredientImportance>? ingredientImportance { get; set; }
        public List<GetPastryMaterialAddOns>? addOns { get; set; }
        public List<GetPastryMaterialSubVariant>? subVariants { get; set; }

        public static GetPastryMaterial DefaultResponse() { return new GetPastryMaterial(); }
        public GetPastryMaterial() { }
        public GetPastryMaterial(PastryMaterials pastryMaterials, List<GetPastryMaterialIngredients> ingredients)
        {
            this.designId = pastryMaterials.design_id;
            this.pastryMaterialId = pastryMaterials.pastry_material_id;
            this.dateAdded = pastryMaterials.date_added;
            this.lastModifiedDate = pastryMaterials.last_modified_date;
            this.ingredients = ingredients;
        }
    }
    public class GetPastryMaterialOtherCost
    {
        public Guid pastryMaterialAdditionalCostId { get; set; }
        public double additionalCost { get; set; }
        public double? ingredientCostMultiplier { get; set; }
    }
    public class GetPastryMaterialIngredients
    {
        [Required] public string ingredientId { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required] public string pastryMaterialId { get; set; }
        [Required] public string ingredientType { get; set; }

        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required] public string itemName { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }

        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }

        public List<SubGetMaterialIngredients>? materialIngredients { get; set; }

    }
    public class GetPastryMaterialIngredientImportance
    {
        [Required] public Guid pastryMaterialIngredientImportanceId { get; set; }
        [Required] public string pastryMaterialId { get; set; }

        [Required] public string itemId { get; set; }
        [Required] public string ingredientType { get; set; }
        [Required] public int importance { get; set; }

        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }
    }
    public class GetPastryMaterialAddOns
    {
        [Required][MaxLength(29)] public string pastryMaterialAddOnId { get; set; }
        [Required][MaxLength(26)] public string pastryMaterialId { get; set; }
        [Required] public int addOnsId { get; set; }
        public string addOnsName { get; set; }
        [Required] public double amount { get; set; }
        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }
    }
    public class GetPastryMaterialSubVariant
    {
        [Required][Key][MaxLength(26)] public string pastryMaterialSubVariantId { get; set; }
        [Required][MaxLength(26)] public string pastryMaterialId { get; set; }

        public string subVariantName { get; set; }

        public double costEstimate { get; set; }
        public double costExactEstimate { get; set; }

        public bool ingredientsInStock { get; set; }
        public List<GetPastryMaterialSubVariantAddOns>? subVariantAddOns { get; set; }
        public List<SubGetPastryMaterialSubVariantIngredients> subVariantIngredients { get; set; }

        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }
    }
    public class GetPastryMaterialSubVariantAddOns
    {
        [Required][MaxLength(26)] public string pastryMaterialSubVariantAddOnId { get; set; }
        [Required][MaxLength(26)] public string pastryMaterialSubVariantId { get; set; }
        [Required] public int addOnsId { get; set; }
        public string addOnsName { get; set; }
        [Required] public double amount { get; set; }
        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }
    }
    public class SubGetPastryMaterialSubVariantIngredients
    {
        [Required][Key][MaxLength(25)] public string pastryMaterialSubVariantIngredientId { get; set; }
        [Required][ForeignKey("PastryMaterialSubVariant")] public string pastryMaterialSubVariantId { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string itemId { get; set; }
        [Required] public string itemName { get; set; }

        //What kind of ingredient is this
        //If wether it is from the inventory or a material
        //Dictates where the API should look up the id in the item_id column
        [Required][MaxLength(3)] public string ingredientType { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amountMeasurement { get; set; }
        public List<SubGetMaterialIngredients>? materialIngredients { get; set; }

        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }
    }

    //BOM Data analysis
    public class GetUsedItemsByOccurence
    {
        [Required] public string itemId { get; set; }
        [Required] public string itemType { get; set; }
        [Required] public string itemName { get; set; }

        [Required] public List<string> asMaterialIngredient { get; set; }
        [Required] public List<string> asCakeIngredient { get; set; }

        public int numOfUsesMaterialIngredient { get; set; }
        public int numOfUsesCakeIngredient { get; set; }

        public double ratioOfUsesMaterialIngredient { get; set; }
        public double ratioOfUsesCakeIngredient { get; set; }
    }
    public class GetUsedItemsBySeasonalTrends
    {
        [Required] public DateTime dateStart { get; set; }
        [Required] public DateTime dateEnd { get; set; }

        [Required] public List<ItemOccurence> itemList { get; set; }
    }
    public class ItemOccurence
    {
        [Required] public string itemId { get; set; }
        [Required] public string itemName { get; set; }
        [Required] public string itemType { get; set; }

        [Required] public int occurrenceCount { get; set; }
        [Required] public double ratio { get; set; }
    }
    public class GetTagOccurrence
    {
        public Guid designTagId { get; set; }
        public string designTagName { get; set; }
        public double occurrenceCount { get; set; }
        public double ratio { get; set; }
    }
    public class GetIngredientSubtractionHistory
    {
        public Guid ingredientSubtractionHistoryId { get; set; }
        public List<GetItemSubtractionInfo> itemSubtractionInfo { get; set;}
        public DateTime dateSubtracted { get; set; }
    }
    public class GetItemSubtractionInfo
    {
        public string itemId { get; set; }
        public string itemName { get; set; }

        public double inventoryPrice { get; set; }
        public string amountQuantityType { get; set; }

        public double inventoryQuantity { get; set; }
        public string inventoryAmountUnit { get; set; }

        public double amount { get; set; }
        public string amountUnit { get; set; }
    }
    public class GetBOMReceipt
    {
        public double totalIngredientPrice { get; set; }
        public double totalIngredientPriceWithOtherCostIncluded { get; set; }
        public double totalIngredientPriceWithOtherCostIncludedRounded { get; set; }
        public List<GetIngredientCostBreakdown>? ingredientCostBreakdown { get; set; }
        public GetOtherCostBreakdown otherCostBreakdown { get; set; }
    }
    public class GetIngredientCostBreakdown
    {
        public string itemId { get; set; }
        public string itemName { get; set; }

        public double inventoryPrice { get; set; }
        public double inventoryQuantity { get; set; }
        public string inventoryAmountUnit { get; set; }


        public string amountQuantityType { get; set; }
        public double amount { get; set; }
        public string amountUnit { get; set; }

        public double calculatedPrice { get; set; }
    }
    public class GetOtherCostBreakdown
    {
        public double additionalCost { get; set; }
        public double ingredientCostMultiplier { get; set; }
    }

    //Design Related
    public class GetDesign
    {
        [Required] public Guid designId { get; set; }
        [Required] public string displayName { get; set; }
        public string? cakeDescription { get; set; }

        public string? designPictureUrl { get; set; }
        public byte[]? displayPictureData { get; set; }

        public List<GetDesignTag>? designTags { get; set; }
        public List<GetDesignShape>? designShapes { get; set; }
    }
    public class GetDesignWithoutPastryMaterial
    {
        [Required] public Guid designId { get; set; }
        [Required] public string displayName { get; set; }
    }
    public class GetDesignWithPastryMaterial
    {
        [Required] public Guid designId { get; set; }
        [Required] public string displayName { get; set; }
        [Required] public string pastryMaterialId { get; set; }
    }
    public class SubGetDesignImage
    {
        public Guid designPictureId { get; set; }
        public byte[] pictureData { get; set; }
    }
    public class GetDesignTag
    {
        public Guid designTagId { get; set; }
        public string designTagName { get; set; }
    }
    public class GetDesignShape
    {
        public Guid designShapeId { get; set; }
        public string shapeName { get; set; }
    }
    public class GetTag
    {
        public string designTagName { get; set; }
    }

    //UI Helpers
    public class GetDesignInfo
    {
        public string pastryMaterialId { get; set; }
        public List<SubGetVariants> variants { get; set; }
    }
    public class SubGetVariants
    {
        public string variantId { get; set; }
        public string variantName { get; set; }
        public double costEstimate { get; set; }
        public bool inStock { get; set; }
        public List<SubGetAddOn> addOns { get; set; }
    }
    public class SubGetAddOn
    {
        public string pastryMaterialAddOnId { get; set; }
        public int addOnId { get; set; }
        public string addOnName { get; set; }
        public double amount { get; set; }
        public double stock { get; set; }
        public double price { get; set; }
    }
}
