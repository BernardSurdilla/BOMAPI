using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Services;
using BillOfMaterialsAPI.Helpers;

using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Text;
using UnitsNet;

namespace BOM_API_v2.Controllers
{
    [ApiController]
    [Route("BOM/materials/")]
    [Authorize(Roles = UserRoles.Admin)]
    public class MaterialController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        private readonly IActionLogger _actionLogger;

        public MaterialController(DatabaseContext context, KaizenTables kaizen, IActionLogger logs)
        {
            _context = context; _actionLogger = logs; _kaizenTables = kaizen;
        }

        //GET
        [HttpGet]
        public async Task<List<GetMaterials>> GetAllMaterials(int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {

            //Default GET request without parameters
            //This should return all records in the BOM database

            //Container for response
            List<GetMaterials> response = new List<GetMaterials>();

            //Get all entries in the 'Materials' and 'MaterialIngredients' table in the connected database
            //And convert it to a list object
            List<Materials> dbMaterials;
            //List<MaterialIngredients> dbMaterialIngredients = await _context.MaterialIngredients.Where(row => row.isActive == true).ToListAsync();

            //Base query for the materials database to retrieve rows
            IQueryable<Materials> materialQuery = _context.Materials.Where(row => row.isActive == true);
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

            if (dbMaterials.IsNullOrEmpty()) { return response; }

            foreach (Materials material in dbMaterials)
            {
                string currentMaterialId = material.material_id;
                List<MaterialIngredients> currentMaterialIngredientsList = await _context.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentMaterialId).ToListAsync();

                SubGetMaterials currentMaterial = new SubGetMaterials(material);
                List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();
                foreach (MaterialIngredients materialIngredients in currentMaterialIngredientsList)
                {
                    SubGetMaterialIngredients newMaterialIngredientResponseForCurrentMaterial = new SubGetMaterialIngredients();
                    switch (materialIngredients.ingredient_type)
                    {
                        case IngredientType.InventoryItem:
                            //!!!UNTESTED!!!
                            //Skip the current material ingredient if it is an inventory item and does not exist in the inventory
                            Item? currentInventoryItemI = null;
                            try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(materialIngredients.item_id)).FirstAsync(); }
                            catch { continue; }
                            if (currentInventoryItemI == null) { continue; }

                            newMaterialIngredientResponseForCurrentMaterial.item_name = currentInventoryItemI.item_name;
                            break;
                        case IngredientType.Material:
                            Materials? currentReferencedMaterial = null;
                            try { currentReferencedMaterial = await _context.Materials.Where(x => x.isActive == true && x.material_id == materialIngredients.item_id).FirstAsync(); }
                            catch { continue; }

                            newMaterialIngredientResponseForCurrentMaterial.item_name = currentReferencedMaterial.material_name;
                            break;
                    }
                    newMaterialIngredientResponseForCurrentMaterial.material_ingredient_id = materialIngredients.material_ingredient_id;
                    newMaterialIngredientResponseForCurrentMaterial.material_id = materialIngredients.material_id;
                    newMaterialIngredientResponseForCurrentMaterial.item_id = materialIngredients.item_id;
                    newMaterialIngredientResponseForCurrentMaterial.ingredient_type = materialIngredients.ingredient_type;
                    newMaterialIngredientResponseForCurrentMaterial.amount = materialIngredients.amount;
                    newMaterialIngredientResponseForCurrentMaterial.amount_measurement = materialIngredients.amount_measurement;
                    newMaterialIngredientResponseForCurrentMaterial.date_added = materialIngredients.date_added;
                    newMaterialIngredientResponseForCurrentMaterial.last_modified_date = materialIngredients.last_modified_date;

                    currentMaterialIngredients.Add(newMaterialIngredientResponseForCurrentMaterial);
                }


                int estimatedCost = 0;
                //Code to calculate cost here
                //
                //

                GetMaterials row = new GetMaterials(currentMaterial, currentMaterialIngredients, estimatedCost);
                response.Add(row);
            }

            await _actionLogger.LogAction(User, "GET", "All materials");
            return response;
        }
        [HttpGet("{material_id}")]
        public async Task<GetMaterials?> GetMaterial(string material_id)
        {
            //GET request with the material_id specified
            //This should return the row with the specified material_id

            GetMaterials response;

            Materials? currentMaterial = await _context.Materials.FindAsync(material_id);

            if (currentMaterial == null) { return new GetMaterials(); }
            if (currentMaterial.isActive == false) { return new GetMaterials(); }

            List<MaterialIngredients> x = await _context.MaterialIngredients.ToListAsync();
            List<MaterialIngredients> y = x.FindAll(x => x.material_id == currentMaterial.material_id && x.isActive == true);

            List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();

            foreach (MaterialIngredients materialIngredients in y)
            {
                SubGetMaterialIngredients newMaterialIngredientResponseForCurrentMaterial = new SubGetMaterialIngredients();

                switch (materialIngredients.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        //!!!UNTESTED!!!
                        //Skip the current material ingredient if it is an inventory item and does not exist in the inventory
                        Item? currentInventoryItemI = null;
                        try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(materialIngredients.item_id)).FirstAsync(); }
                        catch { continue; }
                        if (currentInventoryItemI == null) { continue; }

                        newMaterialIngredientResponseForCurrentMaterial.item_name = currentInventoryItemI.item_name;
                        break;
                    case IngredientType.Material:
                        Materials? currentReferencedMaterial = null;
                        try { currentReferencedMaterial = await _context.Materials.Where(x => x.isActive == true && x.material_id == materialIngredients.item_id).FirstAsync(); }
                        catch { continue; }

                        newMaterialIngredientResponseForCurrentMaterial.item_name = currentReferencedMaterial.material_name;
                        break;
                }

                newMaterialIngredientResponseForCurrentMaterial.material_ingredient_id = materialIngredients.material_ingredient_id;
                newMaterialIngredientResponseForCurrentMaterial.material_id = materialIngredients.material_id;
                newMaterialIngredientResponseForCurrentMaterial.item_id = materialIngredients.item_id;
                newMaterialIngredientResponseForCurrentMaterial.ingredient_type = materialIngredients.ingredient_type;
                newMaterialIngredientResponseForCurrentMaterial.amount = materialIngredients.amount;
                newMaterialIngredientResponseForCurrentMaterial.amount_measurement = materialIngredients.amount_measurement;
                newMaterialIngredientResponseForCurrentMaterial.date_added = materialIngredients.date_added;
                newMaterialIngredientResponseForCurrentMaterial.last_modified_date = materialIngredients.last_modified_date;

                currentMaterialIngredients.Add(newMaterialIngredientResponseForCurrentMaterial);
            }
            int estimatedCost = 0;
            //Code to calculate cost here
            //
            //
            response = new GetMaterials(new SubGetMaterials(currentMaterial), currentMaterialIngredients, estimatedCost);

            await _actionLogger.LogAction(User, "GET", "Material " + currentMaterial.material_id);
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

            if (currentMaterial == null) { response = NotFound(new { message = "Specified material with the material_id is not found or deleted." }); return response; }
            if (currentMaterial.isActive == false) { response = NotFound(new { message = "Specified material with the material_id is not found or deleted." }); return response; }

            switch (column_name)
            {
                case "material_id": response = currentMaterial.material_id; break;
                case "material_name": response = currentMaterial.material_name; break;
                case "amount": response = currentMaterial.amount; break;
                case "amount_measurement": response = currentMaterial.amount_measurement; break;
                case "date_added": response = currentMaterial.date_added; break;
                case "last_modified_date": response = currentMaterial.last_modified_date; break;
                case "cost_estimate":
                    //Replace this with code that calculates the price estimation
                    response = 0;
                    break;
                case "ingredients":
                    List<MaterialIngredients> y = await _context.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentMaterial.material_id).ToListAsync();

                    List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();
                    foreach (MaterialIngredients material in y) { currentMaterialIngredients.Add(new SubGetMaterialIngredients(material)); }
                    response = currentMaterialIngredients;
                    break;

                default: response = BadRequest(new { message = "Specified column does not exist." }); break;
            }

            await _actionLogger.LogAction(User, "GET", "Material " + material_id + " - Column " + column_name);
            return response;
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

                switch (currentType)
                {
                    case IngredientType.InventoryItem:
                        //!!!UNTESTED!!!
                        Item? currentInventoryItemI = null;
                        try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(ing.item_id)).FirstAsync(); }
                        catch (FormatException exF) { return BadRequest(new { message = "Invalid id format for " + ing.item_id + ", must be a value that can be parsed as an integer." }); }
                        catch (InvalidOperationException exO) { return NotFound(new { message = "The id " + ing.item_id + " does not exist in the inventory" }); }

                        if (currentInventoryItemI == null) { return NotFound(new { message = "Item " + ing.item_id + " is not found in the inventory." }); }
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

                try { Materials x = await _context.Materials.OrderByDescending(x => x.material_id).FirstAsync(); lastMaterialId = x.material_id; }
                catch (Exception ex)
                {
                    string newMaterialId = IdFormat.materialIdFormat;
                    for (int i = 1; i <= IdFormat.idNumLength; i++) { newMaterialId += "0"; }
                    lastMaterialId = newMaterialId;
                }

                try { MaterialIngredients x = await _context.MaterialIngredients.OrderByDescending(x => x.material_ingredient_id).FirstAsync(); lastMaterialIngredientId = x.material_ingredient_id; }
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
                newMaterialsEntry.date_added = currentTime;
                newMaterialsEntry.last_modified_date = currentTime;

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
                    currentMatIng.date_added = currentTime;
                    currentMatIng.last_modified_date = currentTime;

                    lastMaterialIngredientId = newMaterialIngredientId;

                    newIngredientsEntry.Add(currentMatIng);
                }

                await _context.Materials.AddAsync(newMaterialsEntry);
                _context.SaveChanges();

                await _context.MaterialIngredients.AddRangeAsync(newIngredientsEntry);
                _context.SaveChanges();

                await _actionLogger.LogAction(User, "POST", "Add Material " + newMaterialsEntry.material_id);
                return Ok(new { message = "Data inserted to the database." });

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error on creating new material." });
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
                        //!!!UNTESTED!!!
                        Item? currentInventoryItemI = null;
                        try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(ing.item_id)).FirstAsync(); }
                        catch (FormatException exF) { return BadRequest(new { message = "Invalid id format for " + ing.item_id + ", must be a value that can be parsed as an integer." }); }
                        catch (InvalidOperationException exO) { return NotFound(new { message = "The id " + ing.item_id + " does not exist in the inventory" }); }

                        if (currentInventoryItemI == null) { return NotFound(new { message = "Item " + ing.item_id + " is not found in the inventory." }); }

                        break;
                    case IngredientType.Material:
                        List<Materials> doesMaterialExist = await _context.Materials.Where(x => x.material_id == ing.item_id && x.isActive == true).ToListAsync();
                        if (doesMaterialExist.IsNullOrEmpty()) { return BadRequest(new { message = "One or more ingredient with the type " + IngredientType.Material + " does not exists in the materials." }); }
                        if (doesMaterialExist.Find(x => x.material_id == material_id) != null) { return BadRequest(new { message = "You cannot put the material as its own material ingredient" }); }

                        List<Materials> materialsToBeChecked = new List<Materials>(doesMaterialExist);

                        //Algorithm for detecting circular references
                        //Replace this method in future if better solution is found
                        int index = 0;
                        bool running = true;
                        while (running)
                        {
                            try
                            {
                                Debug.WriteLine(index);
                                Materials currentMaterial = materialsToBeChecked[index];

                                //Get all "material" type of material_ingredient in the MaterialIngredient Table 
                                List<MaterialIngredients> mIng = await _context.MaterialIngredients.Where(x => x.material_id == currentMaterial.material_id && x.ingredient_type == IngredientType.Material && x.isActive == true).ToListAsync();
                                //Loop through each of them
                                foreach (MaterialIngredients mIngContent in mIng)
                                {
                                    if (mIngContent.item_id == material_id) { return BadRequest(new { message = "Circular reference detected. Material " + material_id + " is a part of " + mIngContent.material_id + "." }); }

                                    Materials newMaterialToCheck = await _context.Materials.Where(x => x.material_id == mIngContent.material_id && x.isActive == true).FirstAsync();
                                    materialsToBeChecked.Add(newMaterialToCheck);

                                }
                                index += 1;
                            }
                            catch { running = false; }
                        }
                        break;
                    default:
                        return BadRequest(new { message = "Ingredients to be inserted has an invalid ingredient_type, valid types are MAT and INV." });
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
                currentMatIng.date_added = currentTime;
                currentMatIng.last_modified_date = currentTime;

                lastMaterialIngredientId = newMaterialIngredientId;

                newMaterialIngredientsEntry.Add(currentMatIng);
            }

            try
            {
                await _context.MaterialIngredients.AddRangeAsync(newMaterialIngredientsEntry);
                _context.SaveChanges();
            }
            catch (Exception ex) { return Problem("Data not inserted to database."); }

            await _actionLogger.LogAction(User, "POST", "Add Material Ingredient to " + material_id);
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
            materialAboutToBeUpdated.last_modified_date = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update Material " + material_id);
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
                    //!!!UNTESTED!!!
                    Item? currentInventoryItemI = null;
                    try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(materialIngredientAboutToBeUpdated.item_id)).FirstAsync(); }
                    catch (FormatException exF) { return BadRequest(new { message = "Invalid id format for " + materialIngredientAboutToBeUpdated.item_id + ", must be a value that can be parsed as an integer." }); }
                    catch (InvalidOperationException exO) { return NotFound(new { message = "The id " + materialIngredientAboutToBeUpdated.item_id + " does not exist in the inventory" }); }

                    if (currentInventoryItemI == null) { return NotFound(new { message = "Item " + materialIngredientAboutToBeUpdated.item_id + " is not found in the inventory." }); }
                    break;
                case IngredientType.Material:
                    List<Materials> doesMaterialExist = await _context.Materials.Where(x => x.material_id == entry.item_id && x.isActive == true).ToListAsync();
                    if (doesMaterialExist.IsNullOrEmpty()) { return BadRequest(new { message = "Material does not exists in the database." }); }
                    break;
                default:
                    return NotFound(new { message = "Something went wrong, this is caused by the invalid entry in the column ingredient_type in the database." }); ;
            }

            DateTime currentTime = DateTime.Now;

            _context.MaterialIngredients.Update(materialIngredientAboutToBeUpdated);
            materialIngredientAboutToBeUpdated.item_id = entry.item_id;
            materialIngredientAboutToBeUpdated.amount = entry.amount;
            materialIngredientAboutToBeUpdated.amount_measurement = entry.amount_measurement;
            materialIngredientAboutToBeUpdated.last_modified_date = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update Material " + material_id + " - Ingredient " + material_ingredient_id);
            return Ok(new { message = "Material ingredient updated." });
        }

        //DELETE
        [HttpDelete("{material_id}")]
        public async Task<IActionResult> DeleteMaterial(string material_id, bool delete_all_pastry_ingredients_connected = false, bool delete_all_material_ingredient_connected = false)
        {
            //Code for deleting materials
            //When deleting materials, all associated MaterialIngredient records will be deleted as well


            //Deleting materials will also delete all of the associated material ingredient
            Materials? materialAboutToBeDeleted = await _context.Materials.FindAsync(material_id);
            if (materialAboutToBeDeleted == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialAboutToBeDeleted.isActive == false) { return NotFound(new { message = "Specified material with the selected material_id already deleted." }); }

            DateTime currentTime = DateTime.Now;
            _context.Materials.Update(materialAboutToBeDeleted);
            materialAboutToBeDeleted.last_modified_date = currentTime;
            materialAboutToBeDeleted.isActive = false;

            List<MaterialIngredients> materialIngredientsAboutToBeDeleted = await _context.MaterialIngredients.ToListAsync();
            materialIngredientsAboutToBeDeleted = materialIngredientsAboutToBeDeleted.FindAll(x => x.isActive == true);
            if (materialIngredientsAboutToBeDeleted.IsNullOrEmpty() == false)
            {
                foreach (MaterialIngredients i in materialIngredientsAboutToBeDeleted)
                {
                    _context.MaterialIngredients.Update(i);
                    i.last_modified_date = currentTime;
                    i.isActive = false;
                }
            }

            //Deleting connected pastry ingredients
            if (delete_all_pastry_ingredients_connected == true)
            {
                List<Ingredients> ingredientsToBeDeleted = await _context.Ingredients.Where(x => x.item_id == materialAboutToBeDeleted.material_id && x.isActive == true).ToListAsync();

                foreach (Ingredients i in ingredientsToBeDeleted)
                {
                    _context.Ingredients.Update(i);
                    i.last_modified_date = currentTime;
                    i.isActive = false;
                }
            }

            //Deleting connected material ingredients of other materials
            if (delete_all_material_ingredient_connected == true)
            {
                List<MaterialIngredients> relatedMaterialIngredientsAboutToBeDeleted = await _context.MaterialIngredients.Where(x => x.item_id == materialAboutToBeDeleted.material_id && x.isActive == true).ToListAsync();

                foreach (MaterialIngredients i in relatedMaterialIngredientsAboutToBeDeleted)
                {
                    _context.MaterialIngredients.Update(i);
                    i.last_modified_date = currentTime;
                    i.isActive = false;
                }
            }

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Delete Material " + material_id + " Also Associated Material&Pastry Ingredient " + delete_all_material_ingredient_connected.ToString() + "/" + delete_all_pastry_ingredients_connected.ToString());
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
            materialIngredientAboutToBeDeleted.last_modified_date = currentTime;
            materialIngredientAboutToBeDeleted.isActive = false;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Material " + material_id + " - Ingredient " + material_ingredient_id);
            return Ok(new { message = "Material ingredient deleted." });
        }
    }
}
