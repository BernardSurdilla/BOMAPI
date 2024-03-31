using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Services;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace BillOfMaterialsAPI.Controllers
{
    [Route("BOM/archive/")]
    [ApiController]
    [Authorize(Roles = UserRoles.Admin)]
    public class ArchiveController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly IActionLogger _actionLogger;

        public ArchiveController(DatabaseContext context, IActionLogger logs)
        { _context = context; _actionLogger = logs; }

        //Getting deleted data
        //GET
        [HttpGet("pastry_materials/")]
        public async Task<List<GetPastryMaterial>> GetAllDeletedPastryMaterial(int? page, int? record_per_page)
        {
            List<PastryMaterials> pastryMaterials;

            List<GetPastryMaterial> response = new List<GetPastryMaterial>();

            //Paging algorithm
            if (page == null) { pastryMaterials = await _context.PastryMaterials.Where(row => row.isActive == false).ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < 10 ? 10 : record_per_page.Value;
                int current_page = page.Value < 1 ? 1 : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                pastryMaterials = await _context.PastryMaterials.Where(row => row.isActive == false).Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            foreach (PastryMaterials i in pastryMaterials) { response.Add(new GetPastryMaterial(i)); }

            await _actionLogger.LogAction(User, "GET, All Deleted Pastry Material ");
            return response;

        }
        [HttpGet("pastry_materials/{pastry_material_id}/ingredients")]
        public async Task<List<GetPastryMaterialIngredients>> GetAllPastryIngredient(string pastry_material_id)
        {
            PastryMaterials? currentPastryMat = await _context.PastryMaterials.FindAsync(pastry_material_id);

            List<Ingredients> currentIngredient = await _context.Ingredients.Where(x => x.pastry_material_id == pastry_material_id && x.isActive == false).ToListAsync();
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
            await _actionLogger.LogAction(User, "GET, All Deleted Pastry Material Ingredients");
            return response;
        }
        [HttpGet("pastry_materials/ingredients/all")]
        public async Task<List<GetIngredients>> GetAllDeletedIngredients(int? page, int? record_per_page)
        {
            List<GetIngredients> response = new List<GetIngredients>();

            List<Ingredients> dbIngredients;

            //Paging algorithm
            if (page == null) { dbIngredients = await _context.Ingredients.Where(row => row.isActive == false).ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < 10 ? 10 : record_per_page.Value;
                int current_page = page.Value < 1 ? 1 : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                dbIngredients = await _context.Ingredients.Where(row => row.isActive == false).Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            if (dbIngredients.IsNullOrEmpty() == true) { response.Add(GetIngredients.DefaultResponse()); return response; }

            foreach (Ingredients i in dbIngredients)
            {
                GetIngredients newRow = new GetIngredients(i.ingredient_id, i.item_id, i.pastry_material_id, i.ingredient_type, i.amount, i.amount_measurement, i.dateAdded, i.lastModifiedDate);
                response.Add(newRow);
            }

            await _actionLogger.LogAction(User, "GET, All Deleted Ingredients");
            return response;
        }

        [HttpGet("materials/")]
        public async Task<List<GetMaterials>> GetDeletedMaterials(int? page, int? record_per_page)
        {
            //Default GET request without parameters
            //This should return all records in the BOM database

            //Container for response
            List<GetMaterials> response = new List<GetMaterials>();

            //Get all entries in the 'Materials' and 'MaterialIngredients' table in the connected database
            //And convert it to a list object
            List<Materials> dbMaterials;
            List<MaterialIngredients> dbMaterialIngredients = await _context.MaterialIngredients.Where(row => row.isActive == false).ToListAsync();

            //Paging algorithm
            if (page == null) { dbMaterials = await _context.Materials.Where(row => row.isActive == false).ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < 10 ? 10 : record_per_page.Value;
                int current_page = page.Value < 1 ? 1 : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                dbMaterials = await _context.Materials.Where(row => row.isActive == false).Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
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

            await _actionLogger.LogAction(User, "GET, All Deleted Materials");
            return response;
        }
        [HttpGet("materials/{material_id}/ingredients")]
        public async Task<List<SubGetMaterialIngredients>> GetDeletedMaterialIngredients(string material_id)
        {
            Materials? currentMaterial = await _context.Materials.FindAsync(material_id);
            //Return empty array if material not found
            if (currentMaterial == null) { return new List<SubGetMaterialIngredients>([new SubGetMaterialIngredients(new MaterialIngredients())]); }

            List<MaterialIngredients> dbMaterialIngredients = await _context.MaterialIngredients.Where(row => row.material_id == currentMaterial.material_id && row.isActive == false).ToListAsync();

            List<SubGetMaterialIngredients> materialIngredients = new List<SubGetMaterialIngredients>();
            //Return empty array if no non active entries are found
            if (dbMaterialIngredients.IsNullOrEmpty() == true) { return new List<SubGetMaterialIngredients>([new SubGetMaterialIngredients(new MaterialIngredients())]); }
            foreach (MaterialIngredients i in dbMaterialIngredients) { materialIngredients.Add(new SubGetMaterialIngredients(i)); }

            await _actionLogger.LogAction(User, "GET, All Deleted Material Ingredients");
            return materialIngredients;
        }

        //
        //Restoring data
        //
        [HttpPatch("pastry_materials/{pastry_material_id}")]
        public async Task<IActionResult> RestorePastryMaterial(string pastry_material_id)
        {
            PastryMaterials? selectedPastryMaterialsEntry = await _context.PastryMaterials.FindAsync(pastry_material_id);
            if (selectedPastryMaterialsEntry == null) { return NotFound(new { message = "Specified pastry materials with the selected pastry_material_id not found." }); }
            if (selectedPastryMaterialsEntry.isActive == true) { return NotFound(new { message = "Specified pastry materials with the selected pastry_material_id still exists." }); }

            _context.PastryMaterials.Update(selectedPastryMaterialsEntry);
            DateTime currentTime = DateTime.Now;
            selectedPastryMaterialsEntry.lastModifiedDate = currentTime;
            selectedPastryMaterialsEntry.isActive = true;

            List<Ingredients> ingredientsOfSelectedEntry = await _context.Ingredients.Where(x => x.pastry_material_id == selectedPastryMaterialsEntry.pastry_material_id && x.isActive == false).ToListAsync();
            if (ingredientsOfSelectedEntry.IsNullOrEmpty() == false)
            {
                foreach (Ingredients i in ingredientsOfSelectedEntry)
                {
                    _context.Ingredients.Update(i);
                    i.lastModifiedDate = currentTime;
                    i.isActive = true;
                }
            }
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Recover Pastry Materials " + pastry_material_id);
            return Ok(new { message = "Pastry Materials restored" });
        }
        [HttpPatch("pastry_materials/{pastry_material_id}/{ingredient_id}")]
        public async Task<IActionResult> RestorePastryMaterialIngredient(string pastry_material_id, string ingredient_id)
        {
            Ingredients? selectedIngredientEntry = await _context.Ingredients.FindAsync(ingredient_id);
            if (selectedIngredientEntry == null) { return NotFound(new { message = "Specified ingredient with the selected ingredient_id not found." }); }
            if (selectedIngredientEntry.isActive == true) { return NotFound(new { message = "Specified ingredient with the selected ingredient_id still exists." }); }

            if (selectedIngredientEntry.pastry_material_id != pastry_material_id) { NotFound(new { message = "Specified ingredient with the selected ingredient_id does not exist in" + pastry_material_id + "." }); }

            _context.Ingredients.Update(selectedIngredientEntry);
            DateTime currentTime = DateTime.Now;
            selectedIngredientEntry.lastModifiedDate = currentTime;
            selectedIngredientEntry.isActive = true;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Recover Ingredient " + ingredient_id);
            return Ok(new { message = "Ingredient restored" });
        }

        //NOTE: When restore all is true
        //It will restore any record that has their associated parent still active
        //For example:
        //Restoring a material that is associated with a pastry material ingredient record will...
        //Restore it if the pastry material connected with that record is active
        //Ignore it if the pastry material connected with that record is not active (deleted)
        [HttpPatch("materials/{material_id}")]
        public async Task<IActionResult> RestoreMaterial(string material_id, bool restore_all_pastry_ingredients_connected = false, bool restore_all_material_ingredient_connected = false)
        {
            //Restoring materials will also restore all of the associated material ingredient

            Materials? materialAboutToBeRestored = await _context.Materials.FindAsync(material_id);
            if (materialAboutToBeRestored == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialAboutToBeRestored.isActive == true) { return NotFound(new { message = "Specified material with the selected material_id still exists." }); }

            DateTime currentTime = DateTime.Now;
            _context.Materials.Update(materialAboutToBeRestored);
            materialAboutToBeRestored.lastModifiedDate = currentTime;
            materialAboutToBeRestored.isActive = true;

            List<MaterialIngredients> materialIngredientsAboutToBeRestored = await _context.MaterialIngredients.Where(x => x.isActive == false && x.material_id == material_id).ToListAsync();
            if (materialIngredientsAboutToBeRestored.IsNullOrEmpty() == false)
            {
                foreach (MaterialIngredients i in materialIngredientsAboutToBeRestored)
                {
                    _context.MaterialIngredients.Update(i);
                    i.lastModifiedDate = currentTime;
                    i.isActive = true;
                }
            }

            //Deleting connected pastry ingredients
            if (restore_all_pastry_ingredients_connected == true)
            {
                List<Ingredients> ingredientsToBeRestored = await _context.Ingredients.Where(x => x.item_id == materialAboutToBeRestored.material_id && x.isActive == false).ToListAsync();

                foreach (Ingredients i in ingredientsToBeRestored)
                {
                    switch (i.ingredient_type)
                    {
                        case IngredientType.InventoryItem:
                            //Add code here to check if the inventory item associated still exist

                            _context.Ingredients.Update(i);
                            i.lastModifiedDate = currentTime;
                            i.isActive = true;
                            break;
                        case IngredientType.Material:
                            //Check if the material associated is active
                            //If it is restore the record
                            PastryMaterials? associatedPastryMaterial = await _context.PastryMaterials.Where(x => x.pastry_material_id == i.pastry_material_id).FirstAsync();
                            
                            if (associatedPastryMaterial != null)
                            {
                                if (associatedPastryMaterial.isActive == true)
                                {
                                    _context.Ingredients.Update(i);
                                    i.lastModifiedDate = currentTime;
                                    i.isActive = true;
                                }
                            }
                            break;
                    }
                }
            }

            //Deleting connected material ingredients of other materials
            if (restore_all_material_ingredient_connected == true)
            {
                List<MaterialIngredients> relatedMaterialIngredientsAboutToBeRestored = await _context.MaterialIngredients.Where(x => x.item_id == materialAboutToBeRestored.material_id && x.isActive == false).ToListAsync();

                foreach (MaterialIngredients i in relatedMaterialIngredientsAboutToBeRestored)
                {
                    switch (i.ingredient_type)
                    {
                        case IngredientType.InventoryItem:
                            //Add code here to check if the inventory item associated still exist

                            _context.MaterialIngredients.Update(i);
                            i.lastModifiedDate = currentTime;
                            i.isActive = true;
                            break;
                        case IngredientType.Material:
                            //Check if the material associated is active
                            //If it is restore the record
                            Materials? associatedMaterial = await _context.Materials.Where(x => x.material_id == i.item_id).FirstAsync();

                            if (associatedMaterial != null)
                            {
                                if (associatedMaterial.isActive == true)
                                {
                                    _context.MaterialIngredients.Update(i);
                                    i.lastModifiedDate = currentTime;
                                    i.isActive = true;
                                }
                            }
                            break;
                    }
                }
            }

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Recover Material " + "Also Associated Material&Pastry Ingredient " + restore_all_material_ingredient_connected.ToString() + "/" + restore_all_pastry_ingredients_connected.ToString());
            return Ok(new { message = "Material restored." });
        }
        [HttpPatch("materials/{material_id}/ingredients/{material_ingredient_id}")]
        public async Task<IActionResult> DeleteMaterialIngredient(string material_id, string material_ingredient_id)
        {
            Materials? materialEntry = await _context.Materials.FindAsync(material_id);
            if (materialEntry == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialEntry.isActive == false) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            MaterialIngredients? materialIngredientAboutToBeDeleted = await _context.MaterialIngredients.FindAsync(material_ingredient_id);
            if (materialIngredientAboutToBeDeleted == null) { return NotFound(new { message = "Specified material ingredient with the selected material_ingredient_id not found." }); }
            if (materialIngredientAboutToBeDeleted.isActive == true) { return NotFound(new { message = "Specified material ingredient with the selected material_ingredient_id still exists." }); }

            DateTime currentTime = DateTime.Now;
            _context.MaterialIngredients.Update(materialIngredientAboutToBeDeleted);
            materialIngredientAboutToBeDeleted.lastModifiedDate = currentTime;
            materialIngredientAboutToBeDeleted.isActive = true;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Recover Material " + material_id + " - Material Ingredient " + material_ingredient_id);
            return Ok(new { message = "Material ingredient restored." });
        }
    }
}