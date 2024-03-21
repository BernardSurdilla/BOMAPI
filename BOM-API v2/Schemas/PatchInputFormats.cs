﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillOfMaterialsAPI.Schemas
{
    public class PatchIngredients
    {
        [Required][MaxLength(25)] public string item_id { get; set; }
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
}
