using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Services;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Security.Claims;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace API_TEST.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public static class IdFormat
    {
        public static string materialIdFormat = "MID";
        public static string materialIngredientIdFormat = "MIID";
        public static string ingredientIdFormat = "IID";
        public static string pastryMaterialIdFormat = "PMID";
        public static string logsIdFormat = "LOG";
        public static int idNumLength = 12;

        public static string IncrementId(string idStringBuffer, int idNumberLength, string idString)
        {
            int index = idString.IndexOf(idStringBuffer);
            string idNumeralsPart = (index < 0) ? idString : idString.Remove(index, idStringBuffer.Length);
            int idInt = Convert.ToInt32(idNumeralsPart);

            int newIdInt = idInt + 1;
            int numberOfNumerals = Convert.ToInt32(newIdInt.ToString()).ToString().Length;

            string newId = newIdInt.ToString();
            for (int i = 0; i < idNumberLength - numberOfNumerals; i++)
            {
                newId = "0" + newId;
            }
            newId = idStringBuffer + newId;
            return newId;
        }
    }

    [ApiController]
    [Route("BOM/")]
    [Authorize(Roles = UserRoles.Admin)]
    public class BOMController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly IActionLogger _actionLogger;

        public BOMController(DatabaseContext context, IActionLogger logger)
        {
            _context = context;
            _actionLogger = logger;
        }

        [HttpGet("item_used/occurence/")]
        public async Task<List<GetUsedItems>> GetMostCommonlyUsedItem()
        {
            List<Ingredients> ingredientsItems = _context.Ingredients.Where(row => row.isActive == true).Select(row => new Ingredients() { item_id = row.item_id, pastry_material_id = row.pastry_material_id }).ToList();

            List<MaterialIngredients> materialIngredientsItems = _context.MaterialIngredients.Where(row => row.isActive == true).Select(row => new MaterialIngredients() { item_id = row.item_id, Materials = row.Materials }).ToList();

            List<GetUsedItems> response = new List<GetUsedItems>();

            //Dictionary<string, string> numberOfUses = new Dictionary<string, string>();

            //Count the ingredients
            using (var ingItemEnum = ingredientsItems.GetEnumerator())
            { 
                while (ingItemEnum.MoveNext())
                {
                    GetUsedItems? currentRecord = response.Find(x => x.item_id == ingItemEnum.Current.item_id);
                    if (currentRecord == null)
                    {
                        GetUsedItems newEntry = new GetUsedItems()
                        {
                            item_id = ingItemEnum.Current.item_id,
                            as_material_ingredient = new List<string>(),
                            as_cake_ingredient = new List<string>(),
                            num_of_uses_cake_ingredient = 0,
                            num_of_uses_material_ingredient = 0
                        };
                        response.Add(newEntry);
                        currentRecord = response.Find(x => x.item_id == ingItemEnum.Current.item_id);
                    }

                    currentRecord.num_of_uses_cake_ingredient += 1;
                    currentRecord.as_cake_ingredient.Add(ingItemEnum.Current.pastry_material_id);
                }
            }
            //Count the material ingredients
            using (var matIngItemEnum = materialIngredientsItems.GetEnumerator())
            {
                while (matIngItemEnum.MoveNext())
                {
                    GetUsedItems? currentRecord = response.Find(x => x.item_id == matIngItemEnum.Current.item_id);
                    if (currentRecord == null)
                    {
                        GetUsedItems newEntry = new GetUsedItems()
                        {
                            item_id = matIngItemEnum.Current.item_id,
                            as_material_ingredient = new List<string>(),
                            as_cake_ingredient = new List<string>(),
                            num_of_uses_cake_ingredient = 0,
                            num_of_uses_material_ingredient = 0
                        };
                        response.Add(newEntry);
                        currentRecord = response.Find(x => x.item_id == matIngItemEnum.Current.item_id);
                    }

                    currentRecord.num_of_uses_material_ingredient += 1;
                    currentRecord.as_material_ingredient.Add(matIngItemEnum.Current.Materials.material_id);
                }
            }
            await _actionLogger.LogAction(User, "GET, Most Commonly Used Items ");
            return response;
        }

        [HttpGet]
        public async Task<string> test()
        {
            string lastPastryMaterialId = "ass";
            try { PastryMaterials x = await _context.PastryMaterials.OrderByDescending(x => x.pastry_material_id).FirstAsync(); lastPastryMaterialId = x.pastry_material_id; }
            catch
            {
                lastPastryMaterialId = "hehehaha";
            }
            return lastPastryMaterialId;
        }
        //Json serialization and deserialization
        //string a = JsonSerializer.Serialize(ingredientAboutToBeDeleted);
        //JsonSerializer.Deserialize<List<SubPastryMaterials_materials_column>>(a);
    }


    [ApiController]
    [Route("BOM/pastry_materials")]
    [Authorize(Roles = UserRoles.Admin)]
    public class PastryIngredientController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly IActionLogger _actionLogger;

        public PastryIngredientController(DatabaseContext context, IActionLogger logs) { _context = context; _actionLogger = logs; }

        //GET
        [HttpGet]
        public async Task<List<GetPastryMaterial>> GetAllPastryMaterial()
        {
            List<PastryMaterials> pastryMaterials = await _context.PastryMaterials.Where(x => x.isActive == true).ToListAsync();

            List<GetPastryMaterial> response = new List<GetPastryMaterial>();

            foreach (PastryMaterials i in  pastryMaterials) { response.Add(new GetPastryMaterial(i)); }

            await _actionLogger.LogAction(User, "GET, All Pastry Material ");
            return response;

        }
        [HttpGet("{pastry_material_id}")]
        public async Task<GetPastryMaterial> GetSpecificPastryMaterial(string pastry_material_id)
        {
            PastryMaterials? currentPastryMat = await _context.PastryMaterials.FindAsync(pastry_material_id);
            if (currentPastryMat == null) { return GetPastryMaterial.DefaultResponse(); }

            GetPastryMaterial response = new GetPastryMaterial(currentPastryMat);

            await _actionLogger.LogAction(User, "GET, Pastry Material " + currentPastryMat.pastry_material_id);
            return response;

        }

        [HttpGet("{pastry_material_id}/ingredients")]
        public async Task<List<GetPastryMaterialIngredients>> GetAllPastryIngredient(string pastry_material_id)
        {
            PastryMaterials? currentPastryMat = await _context.PastryMaterials.FindAsync(pastry_material_id);

            List<Ingredients> currentIngredient = await _context.Ingredients.Where(x => x.pastry_material_id == pastry_material_id && x.isActive == true).ToListAsync();
            List<GetPastryMaterialIngredients> response = new List<GetPastryMaterialIngredients>();

            if (currentIngredient == null || currentPastryMat == null) { return response; }

            foreach (Ingredients i in currentIngredient)
            {
                GetPastryMaterialIngredients newEntry = new GetPastryMaterialIngredients();

                newEntry.ingredient_id = i.ingredient_id;
                newEntry.pastry_material_id = i.pastry_material_id;
                newEntry.ingredient_type = i.ingredient_type;
                newEntry.item_id = i.item_id;
                newEntry.amount = i.amount;
                newEntry.amount_measurement = i.amount_measurement;
                newEntry.dateAdded = i.dateAdded;
                newEntry.lastModifiedDate = i.lastModifiedDate;

                switch (i.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        //
                        //Add code here to find the item in the inventory
                        //
                        break;
                    case IngredientType.Material:
                        List<MaterialIngredients> allMatIng = await _context.MaterialIngredients.Where(x => x.material_id == i.item_id).ToListAsync();

                        List<SubGetMaterialIngredients> materialIngEntry = new List<SubGetMaterialIngredients>(); 
                        foreach (MaterialIngredients mi in allMatIng)
                        {
                            materialIngEntry.Add(new SubGetMaterialIngredients(mi));
                        }
                        newEntry.material_ingredients = materialIngEntry;
                        break;
                    default:
                        break;
                }

                response.Add(newEntry);

            }
            await _actionLogger.LogAction(User, "GET, All Pastry Material Ingredients");
            return response;
        }

        //POST
        [HttpPost]
        public async Task<IActionResult> AddNewPastryMaterial(string designId, List<PostIngredients> entries)
        {
            //Code to check if designID exists here
            //
            //


            foreach (PostIngredients entry in entries)
            {
                switch (entry.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        //
                        //Add code here to find the item in the inventory
                        //
                        break;
                    case IngredientType.Material:
                        //Check if item id exists on the 'Materials' table
                        //or in the inventory
                        var materialIdList = await _context.Materials.Where(x => x.isActive == true).Select(id => id.material_id).ToListAsync();
                        //Add additional code here for inventory id checking
                        if (materialIdList.Find(id => id == entry.item_id).IsNullOrEmpty()) { return NotFound(new { message = "Id specified in the request does not exist in the database." }); }
                        break;
                    default:
                        return BadRequest(new { message = "Ingredients to be inserted has an invalid ingredient_type, valid types are MAT and INV." });
                }
            }

            PastryMaterials newPastryMaterialEntry = new PastryMaterials();

            DateTime currentTime = DateTime.Now;
            string lastPastryMaterialId = "";

            try { PastryMaterials x = await _context.PastryMaterials.OrderByDescending(x => x.pastry_material_id).FirstAsync(); lastPastryMaterialId = x.pastry_material_id; }
            catch (Exception ex)
            {
                string newPastryMaterialId = IdFormat.pastryMaterialIdFormat;
                for (int i = 1; i <= IdFormat.idNumLength; i++) { newPastryMaterialId += "0"; }
                lastPastryMaterialId = newPastryMaterialId;
            }
            string newPastryId = IdFormat.IncrementId(IdFormat.pastryMaterialIdFormat, IdFormat.idNumLength, lastPastryMaterialId);
            lastPastryMaterialId = newPastryId;

            newPastryMaterialEntry.pastry_material_id = newPastryId;
            newPastryMaterialEntry.DesignId = designId;
            newPastryMaterialEntry.dateAdded = currentTime;
            newPastryMaterialEntry.lastModifiedDate = currentTime;
            newPastryMaterialEntry.isActive = true;

            await _context.PastryMaterials.AddAsync(newPastryMaterialEntry);
            await _context.SaveChangesAsync();

            string lastIngredientId = "";
            try { Ingredients x = await _context.Ingredients.OrderByDescending(x => x.ingredient_id).FirstAsync(); lastIngredientId = x.ingredient_id; }
            catch (Exception ex)
            {
                string newIngredientId = IdFormat.ingredientIdFormat;
                for (int i = 1; i <= IdFormat.idNumLength; i++) { newIngredientId += "0"; }
                lastIngredientId = newIngredientId;
            }

            foreach (PostIngredients entry in entries)
            {
                Ingredients newIngredientsEntry = new Ingredients();
                string newId = IdFormat.IncrementId(IdFormat.ingredientIdFormat, IdFormat.idNumLength, lastIngredientId);
                lastIngredientId = newId;

                newIngredientsEntry.ingredient_id = newId;
                newIngredientsEntry.pastry_material_id = lastPastryMaterialId;

                newIngredientsEntry.item_id = entry.item_id;
                newIngredientsEntry.ingredient_type = entry.ingredient_type;

                newIngredientsEntry.amount = entry.amount;
                newIngredientsEntry.amount_measurement = entry.amount_measurement;
                newIngredientsEntry.isActive = true;
                newIngredientsEntry.dateAdded = currentTime;
                newIngredientsEntry.lastModifiedDate = currentTime;

                await _context.Ingredients.AddAsync(newIngredientsEntry);
            }

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST, Add Pastry Materials " + newPastryMaterialEntry.pastry_material_id );
            return Ok(new { message = "Data inserted to the database." });

        }
        [HttpPost("{pastry_material_id}")]
        public async Task<IActionResult> AddNewIngredient(string pastry_material_id, PostIngredients entry)
        {
            PastryMaterials? currentPastryMaterial = await _context.PastryMaterials.Where(x 
                => x.isActive == true).FirstAsync(x => x.pastry_material_id == pastry_material_id);
            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }
            
            switch (entry.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    //
                    //Add code here to find the item in the inventory
                    //
                    break;
                case IngredientType.Material:
                    //Check if item id exists on the 'Materials' table
                    //or in the inventory
                    var materialIdList = await _context.Materials.Where(x => x.isActive == true).Select(id => id.material_id).ToListAsync();
                    //Add additional code here for inventory id checking
                    if (materialIdList.Find(id => id == entry.item_id).IsNullOrEmpty()) { return NotFound(new { message = "Id specified in the request does not exist in the database." }); }
                    break;
                default:
                    return BadRequest(new { message = "Ingredients to be inserted has an invalid ingredient_type, valid types are MAT and INV." });
            }
            string lastIngredientId = "";

            try { Ingredients x = await _context.Ingredients.OrderByDescending(x => x.ingredient_id).FirstAsync(); lastIngredientId = x.ingredient_id; }
            catch (Exception ex)
            {
                string newIngredientId = IdFormat.ingredientIdFormat;
                for (int i = 1; i <= IdFormat.idNumLength; i++) { newIngredientId += "0"; }
                lastIngredientId = newIngredientId;
            }

            Ingredients newIngredientsEntry = new Ingredients();
            DateTime currentTime = DateTime.Now;

            newIngredientsEntry.ingredient_id = IdFormat.IncrementId(IdFormat.ingredientIdFormat, IdFormat.idNumLength, lastIngredientId);
            newIngredientsEntry.pastry_material_id = pastry_material_id;

            newIngredientsEntry.item_id = entry.item_id;
            newIngredientsEntry.ingredient_type = entry.ingredient_type;

            newIngredientsEntry.amount = entry.amount;
            newIngredientsEntry.amount_measurement = entry.amount_measurement;
            newIngredientsEntry.isActive = true;
            newIngredientsEntry.dateAdded = currentTime;
            newIngredientsEntry.lastModifiedDate = currentTime;

            await _context.Ingredients.AddAsync(newIngredientsEntry);
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST, Add Ingredient " + newIngredientsEntry.ingredient_id + " to " + pastry_material_id);
            return Ok(new { message = "Data inserted to the database." });

        }

        //PATCH
        [HttpPatch("{pastry_material_id}")]
        public async Task<IActionResult> UpdatePastryMaterial(string pastry_material_id, PatchPastryMaterials entry)
        {
            //Code to check if design id exists here
            //
            //
            PastryMaterials? currentPastryMaterial = await _context.PastryMaterials.Where(x
                => x.isActive == true).FirstAsync(x => x.pastry_material_id == pastry_material_id);
            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }

            DateTime currentTime = DateTime.Now;

            _context.PastryMaterials.Update(currentPastryMaterial);
            currentPastryMaterial.DesignId = entry.DesignId;
            currentPastryMaterial.lastModifiedDate = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Update Pastry Material " + pastry_material_id);
            return Ok(new { message = "Pastry Material updated." });

        }
        [HttpPatch("{pastry_material_id}/{ingredient_id}")]
        public async Task<IActionResult> UpdatePastryMaterialIngredient(string pastry_material_id, string ingredient_id, PatchIngredients entry)
        {
            //Code to check if design id exists here
            //
            //
            PastryMaterials? currentPastryMaterial = await _context.PastryMaterials.Where(x
                => x.isActive == true).FirstAsync(x => x.pastry_material_id == pastry_material_id);
            Ingredients? currentIngredient = await _context.Ingredients.Where(x => x.isActive == true).FirstAsync(x => x.ingredient_id == ingredient_id);

            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }
            if (currentIngredient == null) { return NotFound(new { message = "No Ingredient with the specified id found." }); }

            switch (entry.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    //
                    //Add code here to find the item in the inventory
                    //
                    break;
                case IngredientType.Material:
                    List<Materials> doesMaterialExist = await _context.Materials.Where(x => x.material_id == entry.item_id && x.isActive == true).ToListAsync();
                    if (doesMaterialExist.IsNullOrEmpty()) { return BadRequest(new { message = "Material does not exists in the database." }); }
                    break;
                default:
                    return NotFound(new { message = "Something went wrong, this is caused by the invalid entry in the column ingredient_type in the database." }); ;
            }

            DateTime currentTime = DateTime.Now;

            _context.Ingredients.Update(currentIngredient);
            currentIngredient.item_id = entry.item_id;
            currentIngredient.amount = entry.amount;
            currentIngredient.amount_measurement = entry.amount_measurement;
            currentIngredient.lastModifiedDate = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Update Pastry Material Ingredient " + pastry_material_id);
            return Ok(new { message = "Pastry Material Ingredient updated." });

        }

        //DELETE
        [HttpDelete("{pastry_material_id}")]
        public async Task<IActionResult> DeletePastryMaterial(string pastry_material_id)
        {
            PastryMaterials? currentPastryMaterial = await _context.PastryMaterials.Where(x => x.pastry_material_id == pastry_material_id && x.isActive == true).FirstAsync();
            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }
            
            DateTime currentTime = DateTime.Now;

            List<Ingredients> ingredients = await _context.Ingredients.Where(x => x.pastry_material_id == pastry_material_id && x.isActive == true).ToListAsync();

            foreach (Ingredients i in ingredients) 
            {
                _context.Ingredients.Update(i);
                i.lastModifiedDate = currentTime;
                i.isActive = false;
            }
            _context.PastryMaterials.Update(currentPastryMaterial);
            currentPastryMaterial.isActive = false;
            currentPastryMaterial.lastModifiedDate = currentTime;

            await _context.SaveChangesAsync();
            await _actionLogger.LogAction(User, "DELETE, Delete Pastry Material " + pastry_material_id);
            return Ok(new { message = "Pastry Material deleted." });
        }
        [HttpDelete("{pastry_material_id}/{ingredient_id}")]
        public async Task<IActionResult> DeletePastryMaterialIngredient(string pastry_material_id, string ingredient_id)
        {
            PastryMaterials? currentPastryMaterial = await _context.PastryMaterials.Where(x
                => x.isActive == true).FirstAsync(x => x.pastry_material_id == pastry_material_id);
            Ingredients? ingredientAboutToBeDeleted = await _context.Ingredients.FindAsync(ingredient_id);
            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }
            if (ingredientAboutToBeDeleted == null) { return NotFound(new { message = "Specified ingredient with the selected ingredient_id not found." }); }
            if (ingredientAboutToBeDeleted.isActive == false) { return NotFound(new { message = "Specified ingredient with the selected ingredient_id already deleted." }); }

            DateTime currentTime = DateTime.Now;
            _context.Ingredients.Update(ingredientAboutToBeDeleted);
            ingredientAboutToBeDeleted.lastModifiedDate = currentTime;
            ingredientAboutToBeDeleted.isActive = false;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE, Delete Pastry Material Ingredient " + ingredient_id);
            return Ok(new { message = "Ingredient deleted." });
        }
    }

    [ApiController]
    [Route("BOM/ingredients/")]
    [Authorize(Roles = UserRoles.Admin)]
    public class IngredientController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly IActionLogger _actionLogger;

        public IngredientController(DatabaseContext context, IActionLogger logs) { _context = context; _actionLogger = logs; }

        // GET
        [HttpGet]
        public async Task<List<GetIngredients>> GetAllIngredients(int? page, int? record_per_page)
        {
            List<GetIngredients> response = new List<GetIngredients>();

            List<Ingredients> dbIngredients;

            //Paging algorithm
            if (page == null) { dbIngredients = await _context.Ingredients.Where(row => row.isActive == true).ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < 10 ? 10 : record_per_page.Value;
                int current_page = page.Value < 1 ? 1 : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                dbIngredients = await _context.Ingredients.Where(row => row.isActive == true).Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }
            if (dbIngredients.IsNullOrEmpty() == true) { response.Add(GetIngredients.DefaultResponse()); return response; }
            foreach (Ingredients i in dbIngredients)
            {
                GetIngredients newRow = new GetIngredients(i.ingredient_id, i.item_id, i.pastry_material_id, i.ingredient_type, i.amount, i.amount_measurement, i.dateAdded, i.lastModifiedDate);
                response.Add(newRow);
            }
            await _actionLogger.LogAction(User, "GET, All ingredients");
            return response;
        }

        [HttpGet("{ingredient_id}/{column_name}")]
        public async Task<object> GetIngredientColumn(string ingredient_id, string column_name)
        {
            Ingredients? currentIngredient = await _context.Ingredients.FindAsync(ingredient_id);
            object? response = null;

            if (currentIngredient == null) { return NotFound(new { message = "Specified material with the material_id is not found." }); }
            if (currentIngredient.isActive == false) { return NotFound(new { message = "Specified material with the material_id is not found or deleted." }); }

            switch (column_name)
            {
                case "ingredient_id": response = currentIngredient.ingredient_id; break;
                case "pastry_material_id": response = currentIngredient.pastry_material_id; break;
                case "item_id": response = currentIngredient.item_id; break;
                case "ingredient_type": response = currentIngredient.pastry_material_id; break;
                case "amount": response = currentIngredient.amount; break;
                case "amount_measurement": response = currentIngredient.amount_measurement; break;
                case "dateAdded": response = currentIngredient.dateAdded; break;
                case "lastModifiedDate": response = currentIngredient.lastModifiedDate; break;
                default: response = BadRequest(new { message = "Specified column does not exist." }); break;
            }

            await _actionLogger.LogAction(User, "GET, Ingredient " + ingredient_id + " - Column " + column_name);
            return response;
        }
    }

    [ApiController]
    [Route("BOM/materials/")]
    [Authorize(Roles = UserRoles.Admin)]
    public class MaterialController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly IActionLogger _actionLogger;

        public MaterialController(DatabaseContext context, IActionLogger logs)
        {
            _context = context; _actionLogger = logs;
        }

        //GET
        [HttpGet]
        public async Task<List<GetMaterials>> GetAllMaterials(int? page, int? record_per_page)
        {

            //Default GET request without parameters
            //This should return all records in the BOM database

            //Container for response
            List<GetMaterials> response = new List<GetMaterials>();

            //Get all entries in the 'Materials' and 'MaterialIngredients' table in the connected database
            //And convert it to a list object
            List<Materials> dbMaterials;
            List<MaterialIngredients> dbMaterialIngredients = await _context.MaterialIngredients.Where(row => row.isActive == true).ToListAsync();

            //Paging algorithm
            if (page == null) { dbMaterials = await _context.Materials.Where(row => row.isActive == true).ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < 10 ? 10 : record_per_page.Value;
                int current_page = page.Value < 1 ? 1 : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                dbMaterials = await _context.Materials.Where(row => row.isActive == true).Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            if (dbMaterials.IsNullOrEmpty()) { response.Add(GetMaterials.DefaultResponse()); return response; }

            foreach (Materials material in dbMaterials)
            {
                string currentMaterialId = material.material_id;
                List<MaterialIngredients> currentMaterialIngredientsList = dbMaterialIngredients.FindAll(x => x.material_id == currentMaterialId);

                SubGetMaterials currentMaterial = new SubGetMaterials(material);
                List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();
                foreach (MaterialIngredients materialIngredients in currentMaterialIngredientsList)
                {
                    SubGetMaterialIngredients x = new SubGetMaterialIngredients(materialIngredients);
                    currentMaterialIngredients.Add(x);
                }


                int estimatedCost = 0;
                //Code to calculate cost here
                //
                //

                GetMaterials row = new GetMaterials(currentMaterial, currentMaterialIngredients, estimatedCost);
                response.Add(row);
            }

            await _actionLogger.LogAction(User, "GET, All materials");
            return response;
        }
        [HttpGet("{material_id}")]
        public async Task<GetMaterials?> GetMaterial(string material_id)
        {
            //GET request with the material_id specified
            //This should return the row with the specified material_id

            GetMaterials response;

            Materials? currentMaterial = await _context.Materials.FindAsync(material_id);

            if (currentMaterial == null) { return GetMaterials.DefaultResponse(); }
            if (currentMaterial.isActive == false) { return GetMaterials.DefaultResponse(); }

            List<MaterialIngredients> x = await _context.MaterialIngredients.ToListAsync();
            List<MaterialIngredients> y = x.FindAll(x => x.material_id == currentMaterial.material_id && x.isActive == true);

            List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();

            foreach (MaterialIngredients material in y)
            {
                SubGetMaterialIngredients z = new SubGetMaterialIngredients(material);
                currentMaterialIngredients.Add(z);
            }
            int estimatedCost = 0;
            //Code to calculate cost here
            //
            //
            response = new GetMaterials(new SubGetMaterials(currentMaterial), currentMaterialIngredients, estimatedCost);

            await _actionLogger.LogAction(User, "GET, Material " + currentMaterial.material_id);
            return response;

            /*
            if (currentMaterial != null)
            {
                if (currentMaterial.isActive == true)
                {
                    List<MaterialIngredients> x = await _context.MaterialIngredients.ToListAsync();
                    List<MaterialIngredients> y = x.FindAll(x => x.material_id == currentMaterial.material_id && x.isActive == true);

                    List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();

                    foreach (MaterialIngredients material in y)
                    {
                        SubGetMaterialIngredients z = new SubGetMaterialIngredients(material);
                        currentMaterialIngredients.Add(z);
                    }
                    int estimatedCost = 0;
                    //Code to calculate cost here
                    //
                    //
                    response = new GetMaterials(new SubGetMaterials(currentMaterial), currentMaterialIngredients, estimatedCost);

                    return response;
                }
                else { return GetMaterials.DefaultResponse(); }
            }
            else { return GetMaterials.DefaultResponse(); }
            */
        }
        [HttpGet("{material_id}/{column_name}")]
        public async Task<object> GetMaterialColumn(string material_id, string column_name)
        {
            Materials? currentMaterial = await _context.Materials.FindAsync(material_id);

            object? response = null;

            if (currentMaterial == null) { response = NotFound(new { message = "Specified material with the material_id is not found or deleted." }); }
            if (currentMaterial.isActive == false) { response = NotFound(new { message = "Specified material with the material_id is not found or deleted." }); }

            switch (column_name)
            {
                case "material_id": response = currentMaterial.material_id; break;
                case "material_name": response = currentMaterial.material_name; break;
                case "amount": response = currentMaterial.amount; break;
                case "amount_measurement": response = currentMaterial.amount_measurement; break;
                case "dateAdded": response = currentMaterial.dateAdded; break;
                case "lastModifiedDate": response = currentMaterial.lastModifiedDate; break;
                case "cost_estimate":
                    //Replace this with code that calculates the price estimation
                    response = 0;
                    break;
                case "material_ingredients":
                    List<MaterialIngredients> y = _context.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentMaterial.material_id).ToList();

                    List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();
                    foreach (MaterialIngredients material in y) { currentMaterialIngredients.Add(new SubGetMaterialIngredients(material)); }
                    response = currentMaterialIngredients;
                    break;

                default: response = BadRequest(new { message = "Specified column does not exist." }); break;
            }

            await _actionLogger.LogAction(User, "GET, Material " + material_id + " - Column " + column_name);
            return response;
            /*
            if (currentMaterial != null)
            {
                if (currentMaterial.isActive == true)
                {
                    switch (column_name)
                    {
                        case "material_id": response = currentMaterial.material_id; break;
                        case "material_name": response = currentMaterial.material_name; break;
                        case "amount": response = currentMaterial.amount; break;
                        case "amount_measurement": response = currentMaterial.amount_measurement; break;
                        case "dateAdded": response = currentMaterial.dateAdded; break;
                        case "lastModifiedDate": response = currentMaterial.lastModifiedDate; break;
                        case "cost_estimate":
                            //Replace this with code that calculates the price estimation
                            response = 0;
                            break;
                        case "material_ingredients":
                            List<MaterialIngredients> x = _context.MaterialIngredients.ToList();
                            List<MaterialIngredients> y = x.FindAll(x => x.material_id == currentMaterial.material_id && x.isActive == true);

                            List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();
                            foreach (MaterialIngredients material in y) { currentMaterialIngredients.Add(new SubGetMaterialIngredients(material)); }
                            response = currentMaterialIngredients;
                            break;

                        default: response = BadRequest(new { message = "Specified column does not exist." }); break;
                    }
                }
            }
            else { response = NotFound(new { message = "Specified material with the material_id is not found or deleted." }); }

            return response;
            */
        }

        //POST
        [HttpPost]
        public async Task<IActionResult> AddMaterialAndMaterialIngredients(PostMaterial_MaterialIngredients data)
        {

            if (data.ingredients.IsNullOrEmpty()) { return BadRequest(new { message = "Ingredients to be inserted is empty or null." }); }

            //Check if the 'item_id' in the 'SubPostMaterialIngredients' list inside of the data from the client 
            //exists in the inventory
            foreach (SubPostMaterialIngredients ing in data.ingredients)
            {
                //Code for checking the id here
                string currentType = ing.ingredient_type;
                if (currentType != IngredientType.InventoryItem && currentType != IngredientType.Material) {  }

                switch (currentType)
                {
                    case IngredientType.InventoryItem:
                        //
                        //Add code here to find the item in the inventory
                        //
                        break;
                    case IngredientType.Material:
                        List<Materials> doesMaterialExist = await _context.Materials.Where(x => x.material_id == ing.item_id && x.isActive == true).ToListAsync();
                        if (doesMaterialExist.IsNullOrEmpty()) { return BadRequest(new { message = "One or more ingredient with the type " + IngredientType.Material + " does not exists in the materials." }); }
                        break;
                    default:
                        return BadRequest(new { message = "Ingredients to be inserted has an invalid ingredient_type, valid types are MAT and INV." });
                }
            }
            try
            {
                Materials newMaterialsEntry = new Materials();
                List<MaterialIngredients> newIngredientsEntry = new List<MaterialIngredients>();

                DateTime currentTime = DateTime.Now;

                string lastMaterialId = "";
                string lastMaterialIngredientId = "";

                try { Materials x = await _context.Materials.LastAsync(); lastMaterialId = x.material_id; }
                catch (Exception ex)
                {
                    string newMaterialId = IdFormat.materialIdFormat;
                    for (int i = 1; i <= IdFormat.idNumLength; i++) { newMaterialId += "0"; }
                    lastMaterialId = newMaterialId;
                }

                try { MaterialIngredients x = await _context.MaterialIngredients.OrderByDescending(x => x.material_id).FirstAsync(); lastMaterialIngredientId = x.material_ingredient_id; }
                catch (Exception ex)
                {
                    string newIngredientId = IdFormat.materialIngredientIdFormat;
                    for (int i = 1; i <= IdFormat.idNumLength; i++) { newIngredientId += "0"; }
                    lastMaterialIngredientId = newIngredientId;
                }

                newMaterialsEntry.material_id = IdFormat.IncrementId(IdFormat.materialIdFormat, IdFormat.idNumLength, lastMaterialId);
                newMaterialsEntry.material_name = data.material_name;
                newMaterialsEntry.amount = data.amount;
                newMaterialsEntry.amount_measurement = data.amount_measurement;

                newMaterialsEntry.isActive = true;
                newMaterialsEntry.dateAdded = currentTime;
                newMaterialsEntry.lastModifiedDate = currentTime;

                foreach (SubPostMaterialIngredients i in data.ingredients)
                {
                    MaterialIngredients currentMatIng = new MaterialIngredients();
                    string newMaterialIngredientId = IdFormat.IncrementId(IdFormat.materialIngredientIdFormat, IdFormat.idNumLength, lastMaterialIngredientId);

                    currentMatIng.material_id = newMaterialsEntry.material_id;
                    currentMatIng.item_id = i.item_id;
                    currentMatIng.ingredient_type = i.ingredient_type;
                    currentMatIng.material_ingredient_id = newMaterialIngredientId;
                    currentMatIng.amount = i.amount;
                    currentMatIng.amount_measurement = i.amount_measurement;

                    currentMatIng.isActive = true;
                    currentMatIng.dateAdded = currentTime;
                    currentMatIng.lastModifiedDate = currentTime;

                    lastMaterialIngredientId = newMaterialIngredientId;

                    newIngredientsEntry.Add(currentMatIng);
                }

                await _context.Materials.AddAsync(newMaterialsEntry);
                _context.SaveChanges();

                await _context.MaterialIngredients.AddRangeAsync(newIngredientsEntry);
                _context.SaveChanges();

                await _actionLogger.LogAction(User, "POST, Add Material " + newMaterialsEntry.material_id);
                return Ok(new { message = "Data inserted to the database." });

            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error on creating new material.");
            }
        }
        [HttpPost("{material_id}/ingredients")]
        public async Task<IActionResult> AddMaterialIngredients(string material_id, List<SubPostMaterialIngredients> materialIngredients)
        {
            if (await _context.Materials.FindAsync(material_id) == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialIngredients == null || materialIngredients.IsNullOrEmpty()) { return BadRequest(new { message = "Material ingredients to be inserted is empty or null." }); }

            //Check if the 'item_id' in the 'SubPostMaterialIngredients' list inside of the data from the client 
            //exists in the inventory
            foreach (SubPostMaterialIngredients ing in materialIngredients)
            {
                //Code for checking the id here
                string currentType = ing.ingredient_type;
                if (currentType != IngredientType.InventoryItem && currentType != IngredientType.Material) { }

                switch (currentType)
                {
                    case IngredientType.InventoryItem:
                        //
                        //Add code here to find the item in the inventory
                        //
                        break;
                    case IngredientType.Material:
                        List<Materials> doesMaterialExist = await _context.Materials.Where(x => x.material_id == ing.item_id && x.isActive == true).ToListAsync();
                        if (doesMaterialExist.IsNullOrEmpty()) { return BadRequest(new { message = "One or more ingredient with the type " + IngredientType.Material + " does not exists in the materials." }); }
                        break;
                    default:
                        return BadRequest(new { message = "Ingredients to be inserted has an invalid ingredient_type, valid types are MAT and INV." });
                        break;
                }
            }

            List<MaterialIngredients> newMaterialIngredientsEntry = new List<MaterialIngredients>();

            string lastMaterialIngredientId = "";
            try { MaterialIngredients x = await _context.MaterialIngredients.OrderByDescending(x => x.material_ingredient_id).FirstAsync(); lastMaterialIngredientId = x.material_ingredient_id; }
            catch (Exception ex)
            {
                string newIngredientId = IdFormat.materialIngredientIdFormat;
                for (int i = 1; i <= IdFormat.idNumLength; i++) { newIngredientId += "0"; }
                lastMaterialIngredientId = newIngredientId;
            }

            foreach (SubPostMaterialIngredients i in materialIngredients)
            {
                MaterialIngredients currentMatIng = new MaterialIngredients();
                string newMaterialIngredientId = IdFormat.IncrementId(IdFormat.materialIngredientIdFormat, IdFormat.idNumLength, lastMaterialIngredientId);

                DateTime currentTime = DateTime.Now;

                currentMatIng.material_id = material_id;
                currentMatIng.item_id = i.item_id;

                currentMatIng.ingredient_type = i.ingredient_type;
                currentMatIng.material_ingredient_id = newMaterialIngredientId;
                currentMatIng.amount = i.amount;
                currentMatIng.amount_measurement = i.amount_measurement;
                currentMatIng.isActive = true;
                currentMatIng.dateAdded = currentTime;
                currentMatIng.lastModifiedDate = currentTime;

                lastMaterialIngredientId = newMaterialIngredientId;

                newMaterialIngredientsEntry.Add(currentMatIng);
            }

            try
            {
                await _context.MaterialIngredients.AddRangeAsync(newMaterialIngredientsEntry);
                _context.SaveChanges();
            }
            catch (Exception ex) { return Problem("Data not inserted to database."); }

            await _actionLogger.LogAction(User, "POST, Add Material Ingredient to " + material_id);
            return Ok(new { message = "Material ingredient inserted to database." });
        }

        //PATCH
        [HttpPatch("{material_id}")]
        public async Task<IActionResult> UpdateMaterials(string material_id, PatchMaterials entry)
        {
            Materials? materialAboutToBeUpdated = await _context.Materials.FindAsync(material_id);
            if (materialAboutToBeUpdated == null) { return NotFound(new { message = "Specified material entry with the selected material_id not found." }); }
            if (materialAboutToBeUpdated.isActive == false) { return NotFound(new { message = "Specified material entry with the selected material_id not found or deleted." }); }

            DateTime currentTime = DateTime.Now;

            _context.Materials.Update(materialAboutToBeUpdated);
            materialAboutToBeUpdated.material_name = entry.material_name;
            materialAboutToBeUpdated.amount = entry.amount;
            materialAboutToBeUpdated.amount_measurement = entry.amount_measurement;
            materialAboutToBeUpdated.lastModifiedDate = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Update Material " + material_id);
            return Ok(new { message = "Material updated." });
        }
        [HttpPatch("{material_id}/ingredients/{material_ingredient_id}")]
        public async Task<IActionResult> UpdateMaterialIngredient(string material_id, string material_ingredient_id, PatchMaterialIngredients entry)
        {
            Materials? materialAboutToBeUpdated = await _context.Materials.FindAsync(material_id);

            if (materialAboutToBeUpdated == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialAboutToBeUpdated.isActive == false) { return NotFound(new { message = "Specified material entry with the selected material_id not found or deleted." }); }
            MaterialIngredients? materialIngredientAboutToBeUpdated = await _context.MaterialIngredients.FindAsync(material_ingredient_id);
            if (materialIngredientAboutToBeUpdated == null) { return NotFound(new { message = "Specified ingredient entry with the selected ingredient_id not found." }); }
            if (materialIngredientAboutToBeUpdated.isActive == false) { return NotFound(new { message = "Specified ingredient entry with the selected ingredient_id not found or deleted." }); }

            //Add code to check if the item_id on the entry exists in the inventory table
            if (entry.item_id == null) { return NotFound(new { message = "Specified item entry with the inputted item_id not found." }); }

            switch (materialIngredientAboutToBeUpdated.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    //
                    //Add code here to find the item in the inventory
                    //
                    break;
                case IngredientType.Material:
                    List<Materials> doesMaterialExist = await _context.Materials.Where(x => x.material_id == entry.item_id && x.isActive == true).ToListAsync();
                    if (doesMaterialExist.IsNullOrEmpty()) { return BadRequest(new { message = "Material does not exists in the database." }); }
                    break;
                default:
                    return NotFound(new { message = "Something went wrong, this is caused by the invalid entry in the column ingredient_type in the database." });;
            }

            DateTime currentTime = DateTime.Now;

            _context.MaterialIngredients.Update(materialIngredientAboutToBeUpdated);
            materialIngredientAboutToBeUpdated.item_id = entry.item_id;
            materialIngredientAboutToBeUpdated.amount = entry.amount;
            materialIngredientAboutToBeUpdated.amount_measurement = entry.amount_measurement;
            materialIngredientAboutToBeUpdated.lastModifiedDate = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Update Material " + material_id + " - Ingredient " + material_ingredient_id);
            return Ok(new { message = "Material ingredient updated." });
        }

        //DELETE
        [HttpDelete("{material_id}")]
        public async Task<IActionResult> DeleteMaterial(string material_id)
        {
            //Deleting materials will also delete all of the associated material ingredient

            Materials? materialAboutToBeDeleted = await _context.Materials.FindAsync(material_id);
            if (materialAboutToBeDeleted == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialAboutToBeDeleted.isActive == false) { return NotFound(new { message = "Specified material with the selected material_id already deleted." }); }

            DateTime currentTime = DateTime.Now;
            _context.Materials.Update(materialAboutToBeDeleted);
            materialAboutToBeDeleted.lastModifiedDate = currentTime;
            materialAboutToBeDeleted.isActive = false;

            List<MaterialIngredients> materialIngredientsAboutToBeDeleted = await _context.MaterialIngredients.ToListAsync();
            materialIngredientsAboutToBeDeleted = materialIngredientsAboutToBeDeleted.FindAll(x => x.isActive == true);
            if (materialIngredientsAboutToBeDeleted.IsNullOrEmpty() == false)
            {
                foreach (MaterialIngredients i in materialIngredientsAboutToBeDeleted)
                {
                    _context.MaterialIngredients.Update(i);
                    i.lastModifiedDate = currentTime;
                    i.isActive = false;
                }
            }
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE, Delete Material " + material_id);
            return Ok(new { message = "Material deleted." });
        }
        [HttpDelete("{material_id}/ingredients/{material_ingredient_id}")]
        public async Task<IActionResult> DeleteMaterialIngredient(string material_id, string material_ingredient_id)
        {
            Materials? materialEntry = await _context.Materials.FindAsync(material_id);
            if (materialEntry == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialEntry.isActive == false) { return NotFound(new { message = "Specified material with the selected material_id not found or deleted." }); }
            MaterialIngredients? materialIngredientAboutToBeDeleted = await _context.MaterialIngredients.FindAsync(material_ingredient_id);
            if (materialIngredientAboutToBeDeleted == null) { return NotFound(new { message = "Specified material ingredient with the selected material_ingredient_id not found." }); }
            if (materialIngredientAboutToBeDeleted.isActive == false) { return NotFound(new { message = "Specified material ingredient with the selected material_ingredient_id already deleted." }); }

            DateTime currentTime = DateTime.Now;
            _context.MaterialIngredients.Update(materialIngredientAboutToBeDeleted);
            materialIngredientAboutToBeDeleted.lastModifiedDate = currentTime;
            materialIngredientAboutToBeDeleted.isActive = false;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE, Material " + material_id + " - Ingredient " + material_ingredient_id);
            return Ok(new { message = "Material ingredient deleted." });
        }
    }


}
