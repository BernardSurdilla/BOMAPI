
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillOfMaterialsAPI.Schemas
{
    public class GetMaterials
    {
        [Required] public SubGetMaterials Material { get; set; }
        [Required] public List<SubGetMaterialIngredients> MaterialIngredients { get; set; }
        public int costEstimate { get; set; }

        public GetMaterials(SubGetMaterials materials, List<SubGetMaterialIngredients> materialIngredients, int costEst)
        {
            Material = materials;
            MaterialIngredients = materialIngredients;
            costEstimate = costEst;
        }
        public static GetMaterials DefaultResponse()
        {

            GetMaterials response = new GetMaterials(new SubGetMaterials(new Materials()), new List<SubGetMaterialIngredients>(), 0);

            return response;
        }
    }
    public class SubGetMaterials
    {
        [Key][Required][MaxLength(25)] public string material_id { get; set; }
        [Required][MaxLength(50)] public string material_name { get; set; }
        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }

        public SubGetMaterials(Materials dbRow)
        {
            this.material_id = dbRow.material_id;
            this.material_name = dbRow.material_name;
            this.amount = dbRow.amount;
            this.amount_measurement = dbRow.amount_measurement;
            this.dateAdded = dbRow.dateAdded;
            this.lastModifiedDate = dbRow.lastModifiedDate;
        }
    }
    public class SubGetMaterialIngredients
    {
        [Required][Key] public string material_ingredient_id { get; set; }
        //To what material this ingredient is connected to
        [Required][ForeignKey("Materials")][MaxLength(25)] public string material_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }

        [Required] public string ingredient_type { get; set; }

        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }

        public SubGetMaterialIngredients(MaterialIngredients dbRow)
        {
            this.material_ingredient_id = dbRow.material_ingredient_id;
            this.material_id = dbRow.material_id;
            this.item_id = dbRow.item_id;
            this.ingredient_type = dbRow.ingredient_type;
            this.amount = dbRow.amount;
            this.amount_measurement = dbRow.amount_measurement;
            this.dateAdded = dbRow.dateAdded;
            this.lastModifiedDate = dbRow.lastModifiedDate;
        }
    }

    public class GetIngredients
    {
        [Required] public string ingredient_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required] public string pastry_material_id { get; set; }

        [Required] public string ingredient_type { get; set; }

        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }


        public GetIngredients(string ingredient_id, string item_id, string pastry_material_id, string ingredient_type, int amount, string amount_measurement, DateTime dateAdded, DateTime lastModifiedDate)
        {
            this.ingredient_id = ingredient_id;
            this.item_id = item_id;
            this.pastry_material_id = pastry_material_id;
            this.ingredient_type = ingredient_type;
            this.amount = amount;
            this.amount_measurement = amount_measurement;
            this.dateAdded = dateAdded;
            this.lastModifiedDate = lastModifiedDate;
        }
        public static GetIngredients DefaultResponse()
        {
            return new GetIngredients("", "", "", "", 0, "", DateTime.MinValue, DateTime.MinValue);
        }
    }
    public class GetPastryMaterial
    {
        [Required][MaxLength(16)] public string DesignId;
        [Required][MaxLength(26)] public string pastry_material_id { get; set; }

        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }

        public static GetPastryMaterial DefaultResponse() { return new GetPastryMaterial(); }
        public GetPastryMaterial() { }
        public GetPastryMaterial(PastryMaterials pastryMaterials)
        {
            this.DesignId = pastryMaterials.DesignId;
            this.pastry_material_id = pastryMaterials.pastry_material_id;
            this.dateAdded = pastryMaterials.dateAdded;
            this.lastModifiedDate = pastryMaterials.lastModifiedDate;
        }
    }

    public class GetPastryMaterialIngredients
    {
        [Required] public string ingredient_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required] public string pastry_material_id { get; set; }

        [Required] public string ingredient_type { get; set; }

        [Required] public int amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required] public DateTime dateAdded { get; set; }
        public DateTime lastModifiedDate { get; set; }

        [Required] public List<SubGetMaterialIngredients> material_ingredients { get; set; }

    }

    public class GetUsedItems
    {
        [Required] public string item_id { get; set; }
        [Required] public List<string> as_material_ingredient { get; set; }
        [Required] public List<string> as_cake_ingredient { get; set; }

        public int num_of_uses_material_ingredient { get; set; }
        public int num_of_uses_cake_ingredient { get; set; }

    }
}
