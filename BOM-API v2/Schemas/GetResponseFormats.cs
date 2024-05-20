
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillOfMaterialsAPI.Schemas
{
    public class GetMaterials
    {
        [Required] public SubGetMaterials Material { get; set; }
        [Required] public List<SubGetMaterialIngredients> material_ingredients { get; set; }
        public double cost_estimate { get; set; }

        public GetMaterials(SubGetMaterials materials, List<SubGetMaterialIngredients> materialIngredients, int costEst)
        {
            Material = materials;
            material_ingredients = materialIngredients;
            cost_estimate = costEst;
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
        [Key][Required][MaxLength(25)] public string material_id { get; set; }
        [Required][MaxLength(50)] public string material_name { get; set; }
        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }

        public SubGetMaterials() { }
        public SubGetMaterials(Materials dbRow)
        {
            this.material_id = dbRow.material_id;
            this.material_name = dbRow.material_name;
            this.amount = dbRow.amount;
            this.amount_measurement = dbRow.amount_measurement;
            this.date_added = dbRow.date_added;
            this.last_modified_date = dbRow.last_modified_date;
        }
    }
    public class SubGetMaterialIngredients
    {
        [Required][Key] public string material_ingredient_id { get; set; }
        //To what material this ingredient is connected to
        [Required][ForeignKey("Materials")][MaxLength(25)] public string material_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required] public string item_name { get; set; }

        [Required] public string ingredient_type { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }
        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }
        
        public SubGetMaterialIngredients() { }
        public SubGetMaterialIngredients(MaterialIngredients dbRow)
        {
            this.material_ingredient_id = dbRow.material_ingredient_id;
            this.material_id = dbRow.material_id;
            this.item_id = dbRow.item_id;
            this.ingredient_type = dbRow.ingredient_type;
            this.amount = dbRow.amount;
            this.amount_measurement = dbRow.amount_measurement;
            this.date_added = dbRow.date_added;
            this.last_modified_date = dbRow.last_modified_date;
        }
    }

    public class GetIngredients
    {
        [Required] public string ingredient_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required] public string pastry_material_id { get; set; }

        [Required] public string ingredient_type { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }

        public GetIngredients() { }
        public GetIngredients(string ingredient_id, string item_id, string pastry_material_id, string ingredient_type, int amount, string amount_measurement, DateTime date_added, DateTime last_modified_date)
        {
            this.ingredient_id = ingredient_id;
            this.item_id = item_id;
            this.pastry_material_id = pastry_material_id;
            this.ingredient_type = ingredient_type;
            this.amount = amount;
            this.amount_measurement = amount_measurement;
            this.date_added = date_added;
            this.last_modified_date = last_modified_date;
        }
        public static GetIngredients DefaultResponse()
        {
            return new GetIngredients();
        }
    }
    public class GetPastryMaterial
    {
        [Required][MaxLength(16)] public byte[] design_id;
        [Required][MaxLength(26)] public string pastry_material_id { get; set; }

        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }

        public double cost_estimate { get; set; }

        public List<GetPastryMaterialIngredients> ingredients { get; set; }

        public static GetPastryMaterial DefaultResponse() { return new GetPastryMaterial(); }
        public GetPastryMaterial() { }
        public GetPastryMaterial(PastryMaterials pastryMaterials, List<GetPastryMaterialIngredients> ingredients)
        {
            this.design_id = pastryMaterials.design_id;
            this.pastry_material_id = pastryMaterials.pastry_material_id;
            this.date_added = pastryMaterials.date_added;
            this.last_modified_date = pastryMaterials.last_modified_date;
            this.ingredients = ingredients;
        }
    }

    public class GetPastryMaterialIngredients
    {
        [Required] public string ingredient_id { get; set; }
        //Which item in the inventory this ingredient pertains to
        [Required] public string pastry_material_id { get; set; }
        [Required] public string ingredient_type { get; set; }

        [Required][MaxLength(25)] public string item_id { get; set; }
        [Required] public string item_name { get; set; }

        [Required] public double amount { get; set; }
        [Required][MaxLength(15)] public string amount_measurement { get; set; }

        [Required] public DateTime date_added { get; set; }
        public DateTime last_modified_date { get; set; }

        [Required] public List<SubGetMaterialIngredients> material_ingredients { get; set; }

    }

    public class GetUsedItemsByOccurence
    {
        [Required] public string item_id { get; set; }
        [Required] public string item_type { get; set; }
        [Required] public string item_name { get; set; }

        [Required] public List<string> as_material_ingredient { get; set; }
        [Required] public List<string> as_cake_ingredient { get; set; }

        public int num_of_uses_material_ingredient { get; set; }
        public int num_of_uses_cake_ingredient { get; set; }

        public double ratio_of_uses_material_ingredient { get; set; }
        public double ratio_of_uses_cake_ingredient { get; set; }
    }
    public class GetUsedItemsBySeasonalTrends
    {
        [Required] public DateTime date_start {  get; set; }
        [Required] public DateTime date_end { get; set; }

        [Required] public List<ItemOccurence> item_list { get; set; }
    }
    public class ItemOccurence
    {
        [Required] public string item_id { get; set; }
        [Required] public string item_name { get; set; }
        [Required] public string item_type { get; set; }

        [Required] public int occurence_count { get; set; }
        [Required] public double ratio { get; set; }
    }
}