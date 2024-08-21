﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillOfMaterialsAPI.Schemas
{
    public class PatchIngredients
    {
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required][MaxLength(3)][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    public class PatchMaterials
    {
        [Required][MaxLength(50)] public string material_name { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    public class PatchMaterialIngredients
    {
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    public class PatchPastryMaterials
    {
        [Required][MaxLength(16)] public byte[] design_id { get; set; }
        [Required] public string main_variant_name { get; set; }
    }

    public class PatchPastryMaterialIngredientImportance
    {
        [Required] public string item_id { get; set; }
        [Required] public string ingredient_type { get; set; }
        [Required] public int importance { get; set; }
    }
    public class PatchPastryMaterialSubVariants
    {
        [Required] public string sub_variant_name { get; set; }
    }
    public class PatchPastryMaterialAddOn
    {
        [Required] public int add_ons_id { get; set; }
        [Required] public double amount { get; set; }
    }
    public class PatchPastryMaterialSubVariantsIngredient
    {
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required][MaxLength(3)][RegularExpression("^(" + IngredientType.Material + "|" + IngredientType.InventoryItem + ")$", ErrorMessage = "Value must be either IngredientType.Material or IngredientType.InventoryItem")] public string ingredient_type { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
    }
    public class PatchPastryMaterialSubVariantAddOn
    {
        [Required] public int add_ons_id { get; set; }
        [Required] public double amount { get; set; }
    }
    public class PatchDesignAddOns
    {
        [Required] public int add_ons_id { get; set; }
        [Required][MaxLength(50)] public string add_on_name { get; set; }
        [Required] public int quantity { get; set; }
        [Required] public double price { get; set; }
    }
    public class PatchDesignShape
    {
        [Required] public Guid design_shape_id { get; set; }
        [Required] public string shape_name { get; set; }
    }
}
