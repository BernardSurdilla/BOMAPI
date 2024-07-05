using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Helpers;

using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Net.NetworkInformation;
using BOM_API_v2.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace BillOfMaterialsAPI.Controllers
{
    [Route("archive/")]
    [ApiController]
    [Authorize(Roles = UserRoles.Admin)]
    public class ArchiveController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        private readonly IActionLogger _actionLogger;

        public ArchiveController(DatabaseContext context, IActionLogger logs)
        { _context = context; _actionLogger = logs; }

        //Getting deleted data
        //GET
        [HttpGet("pastry-materials/")]
        public async Task<List<GetPastryMaterial>> GetAllDeletedPastryMaterial(int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {
            List<PastryMaterials> pastryMaterials;

            List<GetPastryMaterial> response = new List<GetPastryMaterial>();

            //Base query for the materials database to retrieve rows
            IQueryable<PastryMaterials> pastryMaterialQuery = _context.PastryMaterials.Where(row => row.isActive == false);
            //Row sorting algorithm
            if (sortBy != null)
            {
                sortOrder = sortOrder == null ? "ASC" : sortOrder.ToUpper() != "ASC" && sortOrder.ToUpper() != "DESC" ? "ASC" : sortOrder.ToUpper();

                switch (sortBy)
                {
                    case "design_id":
                        switch (sortOrder)
                        {
                            case "DESC":
                                pastryMaterialQuery = pastryMaterialQuery.OrderByDescending(x => x.design_id);
                                break;
                            default:
                                pastryMaterialQuery = pastryMaterialQuery.OrderBy(x => x.design_id);
                                break;
                        }
                        break;
                    case "pastry_material_id":
                        switch (sortOrder)
                        {
                            case "DESC":
                                pastryMaterialQuery = pastryMaterialQuery.OrderByDescending(x => x.pastry_material_id);
                                break;
                            default:
                                pastryMaterialQuery = pastryMaterialQuery.OrderBy(x => x.pastry_material_id);
                                break;
                        }
                        break;
                    case "date_added":
                        switch (sortOrder)
                        {
                            case "DESC":
                                pastryMaterialQuery = pastryMaterialQuery.OrderByDescending(x => x.date_added);
                                break;
                            default:
                                pastryMaterialQuery = pastryMaterialQuery.OrderBy(x => x.date_added);
                                break;
                        }
                        break;
                    case "last_modified_date":
                        switch (sortOrder)
                        {
                            case "DESC":
                                pastryMaterialQuery = pastryMaterialQuery.OrderByDescending(x => x.last_modified_date);
                                break;
                            default:
                                pastryMaterialQuery = pastryMaterialQuery.OrderBy(x => x.last_modified_date);
                                break;
                        }
                        break;
                    default:
                        switch (sortOrder)
                        {
                            case "DESC":
                                pastryMaterialQuery = pastryMaterialQuery.OrderByDescending(x => x.date_added);
                                break;
                            default:
                                pastryMaterialQuery = pastryMaterialQuery.OrderBy(x => x.date_added);
                                break;
                        }
                        break;
                }
            }
            //Paging algorithm
            if (page == null) { pastryMaterials = await pastryMaterialQuery.ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < Page.DefaultNumberOfEntriesPerPage ? Page.DefaultNumberOfEntriesPerPage : record_per_page.Value;
                int current_page = page.Value < Page.DefaultStartingPageNumber ? Page.DefaultStartingPageNumber : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                pastryMaterials = await pastryMaterialQuery.Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            foreach (PastryMaterials i in pastryMaterials) 
            {
                List<Ingredients> ingredientsForCurrentMaterial = await _context.Ingredients.Where(x =>  x.isActive == false && x.pastry_material_id == i.pastry_material_id).ToListAsync();

                List<GetPastryMaterialIngredients> subIngredientList = new List<GetPastryMaterialIngredients>();
                foreach (Ingredients ifcm in ingredientsForCurrentMaterial)
                {
                    GetPastryMaterialIngredients newSubIngredientListEntry = new GetPastryMaterialIngredients();
                    newSubIngredientListEntry.pastry_material_id = ifcm.pastry_material_id;
                    newSubIngredientListEntry.ingredient_id = ifcm.ingredient_id;
                    newSubIngredientListEntry.ingredient_type = ifcm.ingredient_type;
                    newSubIngredientListEntry.amount_measurement = ifcm.amount_measurement;
                    newSubIngredientListEntry.amount = ifcm.amount;
                    newSubIngredientListEntry.item_id = ifcm.item_id;

                    switch (ifcm.ingredient_type)
                    {
                        case IngredientType.InventoryItem:
                            newSubIngredientListEntry.material_ingredients = new List<SubGetMaterialIngredients>();
                            break;
                        case IngredientType.Material:
                            List<MaterialIngredients> currentMaterialReferencedIngredients = await _context.MaterialIngredients.Where(x => x.material_id == ifcm.item_id).ToListAsync();

                            if (!currentMaterialReferencedIngredients.IsNullOrEmpty())
                            {
                                List<SubGetMaterialIngredients> newEntryMaterialIngredients = new List<SubGetMaterialIngredients>();

                                foreach(MaterialIngredients materialIngredients in currentMaterialReferencedIngredients)
                                {
                                    SubGetMaterialIngredients newEntryMaterialIngredientsEntry = new SubGetMaterialIngredients(materialIngredients);
                                    newEntryMaterialIngredients.Add(newEntryMaterialIngredientsEntry);
                                }
                                newSubIngredientListEntry.material_ingredients = newEntryMaterialIngredients;
                            }
                            else
                            {
                                newSubIngredientListEntry.material_ingredients = new List<SubGetMaterialIngredients>();
                            }
                            break;
                    }
                    subIngredientList.Add(newSubIngredientListEntry);
                }

                response.Add(new GetPastryMaterial(i, subIngredientList));
            }

            await _actionLogger.LogAction(User, "GET", "All Deleted Pastry Material ");
            return response;

        }
        [HttpGet("pastry-materials/{pastry-material-id}/ingredients")]
        public async Task<List<GetPastryMaterialIngredients>> GetAllPastryIngredient(string pastry_material_id)
        {
            PastryMaterials? currentPastryMat = await _context.PastryMaterials.FindAsync(pastry_material_id);

            List<Ingredients> currentIngredient = await _context.Ingredients.Where(x => x.pastry_material_id == pastry_material_id && x.isActive == false).ToListAsync();
            List<GetPastryMaterialIngredients> response = new List<GetPastryMaterialIngredients>();

            if (currentIngredient == null || currentPastryMat == null) { return response; }

            foreach (Ingredients i in currentIngredient)
            {
                GetPastryMaterialIngredients newEntry = new GetPastryMaterialIngredients();

                switch (i.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        //!!!UNTESTED!!!
                        Item? currentInventoryItemI = null;
                        try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(i.item_id)).FirstAsync(); }
                        catch (Exception e) { }

                        if (currentInventoryItemI == null) { continue; }

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

                newEntry.ingredient_id = i.ingredient_id;
                newEntry.pastry_material_id = i.pastry_material_id;
                newEntry.ingredient_type = i.ingredient_type;
                newEntry.item_id = i.item_id;
                newEntry.amount = i.amount;
                newEntry.amount_measurement = i.amount_measurement;
                newEntry.date_added = i.date_added;
                newEntry.last_modified_date = i.last_modified_date;

                response.Add(newEntry);

            }
            await _actionLogger.LogAction(User, "GET", "All Deleted Pastry Material Ingredients");
            return response;
        }
        /*
        [HttpGet("materials/")]
        public async Task<List<GetMaterials>> GetDeletedMaterials(int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {
            //Default GET request without parameters
            //This should return all records in the BOM database

            //Container for response
            List<GetMaterials> response = new List<GetMaterials>();

            //Get all entries in the 'Materials' and 'MaterialIngredients' table in the connected database
            //And convert it to a list object
            List<Materials> dbMaterials;
            List<MaterialIngredients> dbMaterialIngredients = await _context.MaterialIngredients.Where(row => row.isActive == false).ToListAsync();

            //Base query for the materials database to retrieve rows
            IQueryable<Materials> materialQuery = _context.Materials.Where(row => row.isActive == false);
            //Row sorting algorithm
            if (sortBy != null)
            {
                sortOrder = sortOrder == null ? "ASC" : sortOrder.ToUpper() != "ASC" && sortOrder.ToUpper() != "DESC" ? "ASC" : sortOrder.ToUpper();

                switch (sortBy)
                {
                    case "material_id":
                        switch (sortOrder)
                        {
                            case "DESC":
                                materialQuery = materialQuery.OrderByDescending(x => x.material_id);
                                break;
                            default:
                                materialQuery = materialQuery.OrderBy(x => x.material_id);
                                break;
                        }
                        break;
                    case "material_name":
                        switch (sortOrder)
                        {
                            case "DESC":
                                materialQuery = materialQuery.OrderByDescending(x => x.material_name);
                                break;
                            default:
                                materialQuery = materialQuery.OrderBy(x => x.material_name);
                                break;
                        }
                        break;
                    case "amount":
                        switch (sortOrder)
                        {
                            case "DESC":
                                materialQuery = materialQuery.OrderByDescending(x => x.amount);
                                break;
                            default:
                                materialQuery = materialQuery.OrderBy(x => x.amount);
                                break;
                        }
                        break;
                    case "amount_measurement":
                        switch (sortOrder)
                        {
                            case "DESC":
                                materialQuery = materialQuery.OrderByDescending(x => x.amount_measurement);
                                break;
                            default:
                                materialQuery = materialQuery.OrderBy(x => x.amount_measurement);
                                break;
                        }
                        break;
                    case "date_added":
                        switch (sortOrder)
                        {
                            case "DESC":
                                materialQuery = materialQuery.OrderByDescending(x => x.date_added);
                                break;
                            default:
                                materialQuery = materialQuery.OrderBy(x => x.date_added);
                                break;
                        }
                        break;
                    case "last_modified_date":
                        switch (sortOrder)
                        {
                            case "DESC":
                                materialQuery = materialQuery.OrderByDescending(x => x.last_modified_date);
                                break;
                            default:
                                materialQuery = materialQuery.OrderBy(x => x.last_modified_date);
                                break;
                        }
                        break;
                    default:
                        switch (sortOrder)
                        {
                            case "DESC":
                                materialQuery = materialQuery.OrderByDescending(x => x.date_added);
                                break;
                            default:
                                materialQuery = materialQuery.OrderBy(x => x.date_added);
                                break;
                        }
                        break;
                }
            }
            //Paging algorithm
            if (page == null) { dbMaterials = await materialQuery.ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < Page.DefaultNumberOfEntriesPerPage ? Page.DefaultNumberOfEntriesPerPage : record_per_page.Value;
                int current_page = page.Value < Page.DefaultStartingPageNumber ? Page.DefaultStartingPageNumber : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                dbMaterials = await materialQuery.Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            if (dbMaterials.IsNullOrEmpty()) { response.Add(new GetMaterials()); return response; }

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

            await _actionLogger.LogAction(User, "GET", "All Deleted Materials");
            return response;
        }
        [HttpGet("materials/{material-id}/ingredients")]
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

            await _actionLogger.LogAction(User, "GET", "All Deleted Material Ingredients");
            return materialIngredients;
        }
        */

        [HttpGet("designs/")]
        public async Task<List<Designs>> GetDeletedDesigns(int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {
            List<Designs> response;

            //Base query for the materials database to retrieve rows
            IQueryable<Designs> designQuery = _context.Designs.Where(row => row.isActive == false);
            //Row sorting algorithm
            if (sortBy != null)
            {
                sortOrder = sortOrder == null ? "ASC" : sortOrder.ToUpper() != "ASC" && sortOrder.ToUpper() != "DESC" ? "ASC" : sortOrder.ToUpper();

                switch (sortBy)
                {
                    case "design_id":
                        switch (sortOrder)
                        {
                            case "DESC":
                                designQuery = designQuery.OrderByDescending(x => x.design_id);
                                break;
                            default:
                                designQuery = designQuery.OrderBy(x => x.design_id);
                                break;
                        }
                        break;
                    case "display_name":
                        switch (sortOrder)
                        {
                            case "DESC":
                                designQuery = designQuery.OrderByDescending(x => x.display_name);
                                break;
                            default:
                                designQuery = designQuery.OrderBy(x => x.display_name);
                                break;
                        }
                        break;
                    case "display_picture_url":
                        switch (sortOrder)
                        {
                            case "DESC":
                                designQuery = designQuery.OrderByDescending(x => x.display_picture_url);
                                break;
                            default:
                                designQuery = designQuery.OrderBy(x => x.display_picture_url);
                                break;
                        }
                        break;
                }
            }
            //Paging algorithm
            if (page == null) { response = await designQuery.ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < Page.DefaultNumberOfEntriesPerPage ? Page.DefaultNumberOfEntriesPerPage : record_per_page.Value;
                int current_page = page.Value < Page.DefaultStartingPageNumber ? Page.DefaultStartingPageNumber : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                response = await designQuery.Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }
            await _actionLogger.LogAction(User, "GET", "All deleted designs");
            return response;
        }
        [HttpGet("designs/{design-id}")]
        public async Task<Designs> GetSpecifiedDeletedDesign(byte[] design_id)
        {
            if (design_id == null) { return new Designs(); }

            try { return await _context.Designs.Where(x => x.isActive == false && x.design_id == design_id).FirstAsync(); }
            catch { return new Designs(); }

        }

        [HttpGet("designs/tags/")]
        public async Task<List<GetDesignTag>> GetDeletedTags(int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {
            IQueryable<DesignTags> dbQuery = _context.DesignTags.Where(x => x.isActive == false);

            List<DesignTags> current_design_records = new List<DesignTags>();
            List<GetDesignTag> response = new List<GetDesignTag>();

            switch (sortBy)
            {
                case "design_tag_id":
                    switch (sortOrder)
                    {
                        case "DESC":
                            dbQuery = dbQuery.OrderByDescending(x => x.design_tag_id);
                            break;
                        default:
                            dbQuery = dbQuery.OrderBy(x => x.design_tag_id);
                            break;
                    }
                    break;
                case "design_tag_name":
                    switch (sortOrder)
                    {
                        case "DESC":
                            dbQuery = dbQuery.OrderByDescending(x => x.design_tag_name);
                            break;
                        default:
                            dbQuery = dbQuery.OrderBy(x => x.design_tag_name);
                            break;
                    }
                    break;
            }

            //Paging algorithm
            if (page == null) { current_design_records = await dbQuery.ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < Page.DefaultNumberOfEntriesPerPage ? Page.DefaultNumberOfEntriesPerPage : record_per_page.Value;
                int current_page = page.Value < Page.DefaultStartingPageNumber ? Page.DefaultStartingPageNumber : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                current_design_records = await dbQuery.Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            foreach (DesignTags currentDesignTag in current_design_records)
            {
                GetDesignTag newResponseEntry = new GetDesignTag();
                newResponseEntry.design_tag_id = currentDesignTag.design_tag_id;
                newResponseEntry.design_tag_name = currentDesignTag.design_tag_name;
                response.Add(newResponseEntry);
            }

            await _actionLogger.LogAction(User, "GET", "All Design tags");
            return response;
        }
        //
        //Restoring data
        //

        //Note: Restoring Pastry Materials will also restore All ingredient entry tied to it
        //If the item or material pointed by the ingredient is not active...
        //It will not restore it
        [HttpPatch("pastry-materials/{pastry_material_id}")]
        public async Task<IActionResult> RestorePastryMaterial(string pastry_material_id)
        {
            PastryMaterials? selectedPastryMaterialsEntry = await _context.PastryMaterials.FindAsync(pastry_material_id);
            if (selectedPastryMaterialsEntry == null) { return NotFound(new { message = "Specified pastry materials with the selected pastry_material_id not found." }); }
            if (selectedPastryMaterialsEntry.isActive == true) { return NotFound(new { message = "Specified pastry materials with the selected pastry_material_id still exists." }); }

            _context.PastryMaterials.Update(selectedPastryMaterialsEntry);
            DateTime currentTime = DateTime.Now;
            selectedPastryMaterialsEntry.last_modified_date = currentTime;
            selectedPastryMaterialsEntry.isActive = true;

            List<string> failedToRestoreEntries = new List<string>();

            List<Ingredients> ingredientsOfSelectedEntry = await _context.Ingredients.Where(x => x.pastry_material_id == selectedPastryMaterialsEntry.pastry_material_id && x.isActive == false).ToListAsync();
            if (ingredientsOfSelectedEntry.IsNullOrEmpty() == false)
            {
                foreach (Ingredients i in ingredientsOfSelectedEntry)
                {
                    switch (i.ingredient_type)
                    {
                        case IngredientType.InventoryItem:
                            //Add code here to check if the inventory item associated still exist

                            _context.Ingredients.Update(i);
                            i.last_modified_date = currentTime;
                            i.isActive = true;
                            break;
                        case IngredientType.Material:
                            Materials? referencedMaterial = await _context.Materials.Where(x => x.material_id == i.item_id).FirstAsync();

                            if (referencedMaterial != null)
                            {
                                if (referencedMaterial.isActive == true)
                                {
                                    _context.Ingredients.Update(i);
                                    i.last_modified_date = currentTime;
                                    i.isActive = true;
                                }
                                else { failedToRestoreEntries.Add("Material: " + referencedMaterial.material_id + " - Ingredient: " + i.ingredient_id); }
                            }

                            break;
                    }
                    
                }
            }
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Recover Pastry Materials " + pastry_material_id);

            if (failedToRestoreEntries.IsNullOrEmpty()) { return Ok(new { message = "Pastry Materials restored. All associated ingredients recovered" }); }
            else { return Ok(new { message = "Pastry Materials restored. Some associated Ingredients failed to be restored however. Check results for more details", results = failedToRestoreEntries }); }
        }
        [HttpPatch("pastry-materials/{pastry_material_id}/{ingredient_id}")]
        public async Task<IActionResult> RestorePastryMaterialIngredient(string pastry_material_id, string ingredient_id)
        {
            PastryMaterials? pastryMaterialEntry = await _context.PastryMaterials.FindAsync(pastry_material_id);
            if (pastryMaterialEntry == null) { return NotFound(new { message = "Specified pastry material with the selected pastry_material_id not found." }); }
            if (pastryMaterialEntry.isActive == false) { return NotFound(new { message = "Specified pastry material with the selected pastry_material_id not found or deleted." }); }
            Ingredients? selectedIngredientEntry = await _context.Ingredients.FindAsync(ingredient_id);
            if (selectedIngredientEntry == null) { return NotFound(new { message = "Specified ingredient with the selected ingredient_id not found." }); }
            if (selectedIngredientEntry.isActive == true) { return BadRequest(new { message = "Specified ingredient with the selected ingredient_id still exists." }); }

            if (selectedIngredientEntry.pastry_material_id != pastry_material_id) { NotFound(new { message = "Specified ingredient with the selected ingredient_id does not exist in" + pastry_material_id + "." }); }

            DateTime currentTime = DateTime.Now;

            switch (selectedIngredientEntry.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    //Add code here to check if the inventory item associated still exist

                    _context.Ingredients.Update(selectedIngredientEntry);
                    selectedIngredientEntry.last_modified_date = currentTime;
                    selectedIngredientEntry.isActive = true;
                    break;
                case IngredientType.Material:
                    Materials? referencedMaterial = await _context.Materials.Where(x => x.material_id == selectedIngredientEntry.item_id).FirstAsync();

                    if (referencedMaterial != null)
                    {
                        if (referencedMaterial.isActive == true)
                        {
                            _context.Ingredients.Update(selectedIngredientEntry);
                            selectedIngredientEntry.last_modified_date = currentTime;
                            selectedIngredientEntry.isActive = true;
                        }
                        else
                        {
                            return BadRequest(new { message = "Specified pastry material ingredient found, but cannot be restored as the material it refers to is deleted. Material " + referencedMaterial.material_id });
                        }
                    }
                    break;
            }
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Recover Ingredient " + ingredient_id);
            return Ok(new { message = "Ingredient restored" });
        }

        /*
        //NOTE: When restore all is true
        //It will restore any record that references this material if their associated parent is active
        //
        //For example:
        //Restoring a material that is associated with a pastry material ingredient record will...
        //Restore the pastry material ingredient connected associated with the material IF the pastry material for that ingredient is active
        //Ignore the pastry material ingredient connected associated with the material IF the pastry material for that ingredient is not active (deleted)
        //
        //Same goes for the material ingredients
        [HttpPatch("materials/{material_id}")]
        public async Task<IActionResult> RestoreMaterial(string material_id, bool restore_all_pastry_ingredients_connected = false, bool restore_all_material_ingredient_connected = false)
        {
            //Restoring materials will also restore all of the associated material ingredient

            Materials? materialAboutToBeRestored = await _context.Materials.FindAsync(material_id);
            if (materialAboutToBeRestored == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialAboutToBeRestored.isActive == true) { return NotFound(new { message = "Specified material with the selected material_id still exists." }); }

            DateTime currentTime = DateTime.Now;
            _context.Materials.Update(materialAboutToBeRestored);
            materialAboutToBeRestored.last_modified_date = currentTime;
            materialAboutToBeRestored.isActive = true;

            List<string> failedToRestoreEntries = new List<string>();

            List<MaterialIngredients> materialIngredientsAboutToBeRestored = await _context.MaterialIngredients.Where(x => x.isActive == false && x.material_id == material_id).ToListAsync();
            if (materialIngredientsAboutToBeRestored.IsNullOrEmpty() == false)
            {
                foreach (MaterialIngredients i in materialIngredientsAboutToBeRestored)
                {
                    switch (i.ingredient_type)
                    {
                        case IngredientType.InventoryItem:
                            //Add code here to check if the inventory item associated still exist

                            _context.MaterialIngredients.Update(i);
                            i.last_modified_date = currentTime;
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
                                    i.last_modified_date = currentTime;
                                    i.isActive = true;
                                }
                                else { failedToRestoreEntries.Add("Material Ingredient: " + i.material_ingredient_id + " - Material: " + associatedMaterial.material_id); }
                            }
                            break;
                    }
                }
            }

            

            //Restoring connected pastry ingredients
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
                            i.last_modified_date = currentTime;
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
                                    i.last_modified_date = currentTime;
                                    i.isActive = true;
                                }
                                else { failedToRestoreEntries.Add("Pastry Material: " + associatedPastryMaterial.pastry_material_id + " - Ingredient: " + i.ingredient_id ); }
                            }
                            break;
                    }
                }
            }
            //Restoring connected material ingredients of other materials
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
                            i.last_modified_date = currentTime;
                            i.isActive = true;
                            break;
                        case IngredientType.Material:
                            //Check if the material associated is active
                            //If it is restore the record
                            Materials? associatedMaterial = await _context.Materials.Where(x => x.material_id == i.material_id).FirstAsync();

                            if (associatedMaterial != null)
                            {
                                if (associatedMaterial.isActive == true)
                                {
                                    _context.MaterialIngredients.Update(i);
                                    i.last_modified_date = currentTime;
                                    i.isActive = true;
                                }
                                else { failedToRestoreEntries.Add("Material: " + associatedMaterial.material_id + " - Material Ingredient: " + i.material_ingredient_id); }
                            }
                            break;
                    }
                }
            }

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Recover Material " + "Also Associated Material/Pastry Ingredient? " + restore_all_material_ingredient_connected.ToString() + "/" + restore_all_pastry_ingredients_connected.ToString());

            if (failedToRestoreEntries.IsNullOrEmpty() ) { return Ok(new { message = "Material restored. All associated Material Ingredient and Pastry Material Ingredient restored" }); }
            else { return Ok(new { message = "Material restored. Some associated Material Ingredient and/or Pastry Material Ingredient failed to be restored however. Check results for more details", results = failedToRestoreEntries }); }
            
        }
        [HttpPatch("materials/{material_id}/ingredients/{material_ingredient_id}")]
        public async Task<IActionResult> RestoreMaterialIngredient(string material_id, string material_ingredient_id)
        {
            Materials? materialEntry = await _context.Materials.FindAsync(material_id);
            if (materialEntry == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialEntry.isActive == false) { return NotFound(new { message = "Specified material with the selected material_id not found or deleted." }); }
            MaterialIngredients? materialIngredientAboutToBeRestored = await _context.MaterialIngredients.FindAsync(material_ingredient_id);
            if (materialIngredientAboutToBeRestored == null) { return NotFound(new { message = "Specified material ingredient with the selected material_ingredient_id not found." }); }
            if (materialIngredientAboutToBeRestored.isActive == true) { return BadRequest(new { message = "Specified material ingredient with the selected material_ingredient_id still exists." }); }

            DateTime currentTime = DateTime.Now;

            switch (materialIngredientAboutToBeRestored.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    //Add code here to check if the inventory item associated still exist

                    _context.MaterialIngredients.Update(materialIngredientAboutToBeRestored);
                    materialIngredientAboutToBeRestored.last_modified_date = currentTime;
                    materialIngredientAboutToBeRestored.isActive = true;

                    break;
                case IngredientType.Material:
                    Materials? referencedMaterial = await _context.Materials.Where(x => x.material_id == materialIngredientAboutToBeRestored.item_id).FirstAsync();

                    if (referencedMaterial != null)
                    {
                        if (referencedMaterial.isActive == true)
                        {
                            _context.MaterialIngredients.Update(materialIngredientAboutToBeRestored);
                            materialIngredientAboutToBeRestored.last_modified_date = currentTime;
                            materialIngredientAboutToBeRestored.isActive = true;
                        }
                        else
                        {
                            return BadRequest(new { message = "Specified material ingredient found, but cannot be restored as the material it refers to is deleted. Material " + referencedMaterial.material_id });
                        }
                    }
                    break;
            }

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Recover Material " + material_id + " - Material Ingredient " + material_ingredient_id);
            return Ok(new { message = "Material ingredient restored." });
        }
        */

        [HttpPatch("designs/{design-id}")]
        public async Task<IActionResult> RestoreDesign(byte[] design_id)
        {
            Designs? selectedRow;
            try { selectedRow = await _context.Designs.Where(x => x.isActive == false && x.design_id == design_id).FirstAsync(); }
            catch { return NotFound(new { message = "Design with the specified id not found" }); }

            _context.Designs.Update(selectedRow);
            selectedRow.isActive = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Design restored sucessfully" });
        }
        [HttpPatch("designs/tags/{design-tag-id}")]
        public async Task<IActionResult> RecoverDesignTag(Guid design_tag_id)
        {
            DesignTags? selectedDesignTag;
            try { selectedDesignTag = await _context.DesignTags.Where(x => x.isActive == false && x.design_tag_id == design_tag_id).FirstAsync(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = "Specified design tag with the id " + design_tag_id + " does not exist" }); }
            catch (Exception e) { return BadRequest(new { message = "An unspecified error occured when retrieving the data" }); }

            _context.DesignTags.Update(selectedDesignTag);
            selectedDesignTag.isActive = true;

            await _context.SaveChangesAsync();
            _actionLogger.LogAction(User, "DELETE", "Delete design tag " + selectedDesignTag.design_tag_id);
            return Ok(new { message = "Design " + selectedDesignTag.design_tag_id + " deleted" });
        }
    }
}