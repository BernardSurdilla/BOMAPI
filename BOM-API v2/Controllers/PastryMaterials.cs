﻿using BillOfMaterialsAPI.Models;
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
    [Route("BOM/pastry_materials")]
    [Authorize(Roles = UserRoles.Admin)]
    public class PastryMaterialController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        private readonly IActionLogger _actionLogger;

        public PastryMaterialController(DatabaseContext context, KaizenTables kaizen, IActionLogger logs) { _context = context; _actionLogger = logs; _kaizenTables = kaizen; }

        //GET
        [HttpGet]
        public async Task<List<GetPastryMaterial>> GetAllPastryMaterial(int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {
            List<PastryMaterials> pastryMaterials;
            List<GetPastryMaterial> response = new List<GetPastryMaterial>();
            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients

            //Base query for the materials database to retrieve rows
            IQueryable<PastryMaterials> pastryMaterialQuery = _context.PastryMaterials.Where(row => row.isActive == true);
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

            foreach(PastryMaterials currentPastryMaterial in pastryMaterials)
            {
                GetPastryMaterial newRow;
                try { newRow = await CreatePastryMaterialResponseFromDBRow(currentPastryMaterial); }
                catch { continue; }
                response.Add(newRow);
            }

            await _actionLogger.LogAction(User, "GET", "All Pastry Material ");
            return response;
        }
        [HttpGet("{pastry_material_id}")]
        public async Task<GetPastryMaterial> GetSpecificPastryMaterial(string pastry_material_id)
        {
            PastryMaterials? currentPastryMat = await _context.PastryMaterials.FindAsync(pastry_material_id);
            if (currentPastryMat == null) { return new GetPastryMaterial(); }

            List<Ingredients> ingredientsForCurrentMaterial = await _context.Ingredients.Where(x => x.isActive == true && x.pastry_material_id == currentPastryMat.pastry_material_id).ToListAsync();
            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients

            GetPastryMaterial response;
            try { response = await CreatePastryMaterialResponseFromDBRow(currentPastryMat); }
            catch (InvalidOperationException e) { return new GetPastryMaterial(); }

            await _actionLogger.LogAction(User, "GET", "Pastry Material " + currentPastryMat.pastry_material_id);
            return response;

        }
        [HttpGet("{pastry_material_id}/ingredients")]
        public async Task<List<GetPastryMaterialIngredients>> GetAllPastryMaterialIngredient(string pastry_material_id)
        {
            PastryMaterials? currentPastryMat = await _context.PastryMaterials.FindAsync(pastry_material_id);

            List<Ingredients> currentIngredient = await _context.Ingredients.Where(x => x.pastry_material_id == pastry_material_id && x.isActive == true).ToListAsync();
            List<GetPastryMaterialIngredients> response = new List<GetPastryMaterialIngredients>();

            if (currentIngredient == null || currentPastryMat == null) { return response; }

            GetPastryMaterial encodedDbRow;
            try { encodedDbRow = await CreatePastryMaterialResponseFromDBRow(currentPastryMat); }
            catch { return response; }
            response = encodedDbRow.ingredients;

            await _actionLogger.LogAction(User, "GET", "All Pastry Material Ingredients");
            return response;
        }

        [HttpGet("by_design_id/{designId}")]
        public async Task<GetPastryMaterial> GetSpecificPastryMaterialByDesignId([FromRoute]byte[] designId)
        {
            PastryMaterials? currentPastryMat = null;
            try { currentPastryMat = await _context.PastryMaterials.Where(x => x.isActive == true && x.design_id == designId).FirstAsync(); }
            catch (Exception e) { return new GetPastryMaterial(); }


            List<Ingredients> ingredientsForCurrentMaterial = await _context.Ingredients.Where(x => x.isActive == true && x.pastry_material_id == currentPastryMat.pastry_material_id).ToListAsync();
            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients

            List<GetPastryMaterialIngredients> subIngredientList = new List<GetPastryMaterialIngredients>();

            double calculatedCost = 0.0;

            foreach (Ingredients ifcm in ingredientsForCurrentMaterial)
            {
                GetPastryMaterialIngredients newSubIngredientListEntry = new GetPastryMaterialIngredients();

                //Check if the measurement unit in the ingredient record is valid
                //If not found, skip current ingredient
                string? amountQuantityType = null;
                string? amountUnitMeasurement = null;

                bool isAmountMeasurementValid = false;
                foreach (string unitQuantity in validMeasurementUnits.Keys)
                {
                    List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                    string? currentMeasurement = currentQuantityUnits.Find(x => x.Equals(ifcm.amount_measurement));

                    if (currentMeasurement == null) { continue; }
                    else
                    {
                        isAmountMeasurementValid = true;
                        amountQuantityType = unitQuantity;
                        amountUnitMeasurement = currentMeasurement;
                    }
                }
                if (isAmountMeasurementValid == false)
                {
                    return new GetPastryMaterial(); //This should return something to identify the error
                }

                switch (ifcm.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        {
                            //!!!UNTESTED!!!
                            Item? currentInventoryItemI = null;
                            try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(ifcm.item_id)).FirstAsync(); }
                            catch { continue; }
                            if (currentInventoryItemI == null) { continue; }

                            newSubIngredientListEntry.item_name = currentInventoryItemI.item_name;
                            newSubIngredientListEntry.material_ingredients = new List<SubGetMaterialIngredients>();

                            double convertedAmountI = 0.0;
                            double calculatedAmountI = 0.0;
                            if (amountQuantityType != "Count")
                            {
                                convertedAmountI = UnitConverter.ConvertByName(ifcm.amount, amountQuantityType, amountUnitMeasurement, currentInventoryItemI.measurements);
                                calculatedAmountI = convertedAmountI * currentInventoryItemI.price;
                            }
                            else
                            {
                                convertedAmountI = ifcm.amount;
                                calculatedAmountI = convertedAmountI * currentInventoryItemI.price;
                            }

                            calculatedCost += calculatedAmountI;

                            break;
                        }
                    case IngredientType.Material:
                        {
                            Materials? currentReferencedMaterial = await _context.Materials.Where(x => x.material_id == ifcm.item_id && x.isActive == true).FirstAsync();
                            if (currentPastryMat == null) { continue; }

                            newSubIngredientListEntry.item_name = currentReferencedMaterial.material_name;
                            List<MaterialIngredients> currentMaterialReferencedIngredients = await _context.MaterialIngredients.Where(x => x.material_id == ifcm.item_id).ToListAsync();

                            if (!currentMaterialReferencedIngredients.IsNullOrEmpty())
                            {
                                List<SubGetMaterialIngredients> newEntryMaterialIngredients = new List<SubGetMaterialIngredients>();

                                foreach (MaterialIngredients materialIngredients in currentMaterialReferencedIngredients)
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
                            //Price calculation code
                            //Get all ingredient for currently referenced material
                            List<MaterialIngredients> subIngredientsForCurrentIngredient = currentMaterialReferencedIngredients.Where(x => x.ingredient_type == IngredientType.InventoryItem).ToList();
                            double currentSubIngredientCostMultiplier = amountUnitMeasurement.Equals(currentReferencedMaterial.amount_measurement) ? currentReferencedMaterial.amount / ifcm.amount : currentReferencedMaterial.amount / UnitConverter.ConvertByName(ifcm.amount, amountQuantityType, amountUnitMeasurement, currentReferencedMaterial.amount_measurement);
                            foreach (MaterialIngredients subIng in subIngredientsForCurrentIngredient)
                            {
                                Item? currentReferencedIngredientM = null;
                                try { currentReferencedIngredientM = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(subIng.item_id)).FirstAsync(); }
                                catch (Exception e) { Console.WriteLine("Error in retrieving " + subIng.item_id + " on inventory: " + e.GetType().ToString()); continue; }

                                double currentRefItemPrice = currentReferencedIngredientM.price;
                                double ingredientCost = currentReferencedIngredientM.measurements == subIng.amount_measurement ? (currentRefItemPrice * ifcm.amount) * currentSubIngredientCostMultiplier : (currentRefItemPrice * UnitConverter.ConvertByName(ifcm.amount, amountQuantityType, amountUnitMeasurement, currentReferencedIngredientM.measurements) * currentSubIngredientCostMultiplier);

                                calculatedCost += ingredientCost;
                            }

                            //Get All material types of ingredient of the current ingredient
                            List<MaterialIngredients> subMaterials = currentMaterialReferencedIngredients.Where(x => x.ingredient_type == IngredientType.Material).ToList();
                            int subMaterialIngLoopIndex = 0;
                            bool isLoopingThroughSubMaterials = true;

                            while (isLoopingThroughSubMaterials)
                            {
                                MaterialIngredients currentSubMaterial;
                                try { currentSubMaterial = subMaterials[subMaterialIngLoopIndex]; }
                                catch (Exception e) { isLoopingThroughSubMaterials = false; break; }

                                Materials currentReferencedMaterialForSub = await _context.Materials.Where(x => x.isActive == true && x.material_id == currentSubMaterial.item_id).FirstAsync();

                                string refMatMeasurement = currentReferencedMaterialForSub.amount_measurement;
                                double refMatAmount = currentReferencedMaterialForSub.amount;

                                string subMatMeasurement = currentSubMaterial.amount_measurement;
                                double subMatAmount = currentSubMaterial.amount;

                                string measurementQuantity = "";

                                foreach (string unitQuantity in validMeasurementUnits.Keys)
                                {
                                    List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                                    string? currentSubMatMeasurement = currentQuantityUnits.Find(x => x.Equals(subMatMeasurement));
                                    string? currentRefMatMeasurement = currentQuantityUnits.Find(x => x.Equals(refMatMeasurement));

                                    if (currentSubMatMeasurement != null && currentRefMatMeasurement != null) { measurementQuantity = unitQuantity; }
                                    else { continue; }
                                }

                                double costMultiplier = refMatMeasurement == subMatMeasurement ? refMatAmount / subMatAmount : refMatAmount / UnitConverter.ConvertByName(subMatAmount, measurementQuantity, subMatMeasurement, refMatMeasurement);

                                List<MaterialIngredients> subMaterialIngredients = await _context.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentReferencedMaterialForSub.material_id).ToListAsync();
                                foreach (MaterialIngredients subMaterialIngredientsRow in subMaterialIngredients)
                                {
                                    switch (subMaterialIngredientsRow.ingredient_type)
                                    {
                                        case IngredientType.InventoryItem:
                                            Item? refItemForSubMatIng = null;
                                            try { refItemForSubMatIng = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(subMaterialIngredientsRow.item_id)).FirstAsync(); }
                                            catch (Exception e) { Console.WriteLine("Error in retrieving " + subMaterialIngredientsRow.item_id + " on inventory: " + e.GetType().ToString()); continue; }

                                            string subMatIngRowMeasurement = subMaterialIngredientsRow.amount_measurement;
                                            double subMatIngRowAmount = subMaterialIngredientsRow.amount;

                                            string refItemMeasurement = refItemForSubMatIng.measurements;
                                            double refItemPrice = refItemForSubMatIng.price;

                                            string refItemQuantityUnit = "";
                                            foreach (string unitQuantity in validMeasurementUnits.Keys)
                                            {
                                                List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                                                string? currentSubMatMeasurement = currentQuantityUnits.Find(x => x.Equals(subMatIngRowMeasurement));
                                                string? currentRefMatMeasurement = currentQuantityUnits.Find(x => x.Equals(refItemMeasurement));

                                                if (currentSubMatMeasurement != null && currentRefMatMeasurement != null) { refItemQuantityUnit = unitQuantity; }
                                                else { continue; }
                                            }

                                            double currentSubMaterialIngredientPrice = refItemForSubMatIng.measurements == subMaterialIngredientsRow.amount_measurement ? (refItemPrice * subMatIngRowAmount) * costMultiplier : (refItemPrice * UnitConverter.ConvertByName(subMatIngRowAmount, refItemQuantityUnit, subMatIngRowMeasurement, refItemMeasurement)) * costMultiplier;

                                            calculatedCost += currentSubMaterialIngredientPrice;
                                            break;
                                        case IngredientType.Material:
                                            subMaterials.Add(subMaterialIngredientsRow);
                                            break;
                                    }
                                }
                                subMaterialIngLoopIndex += 1;

                                break;
                            }
                            break;
                        }
                }

                newSubIngredientListEntry.pastry_material_id = ifcm.pastry_material_id;
                newSubIngredientListEntry.ingredient_id = ifcm.ingredient_id;
                newSubIngredientListEntry.ingredient_type = ifcm.ingredient_type;
                newSubIngredientListEntry.amount_measurement = ifcm.amount_measurement;
                newSubIngredientListEntry.amount = ifcm.amount;
                newSubIngredientListEntry.item_id = ifcm.item_id;

                subIngredientList.Add(newSubIngredientListEntry);
            }

            GetPastryMaterial response = new GetPastryMaterial(currentPastryMat, subIngredientList);
            response.cost_estimate = calculatedCost;

            await _actionLogger.LogAction(User, "GET", "Pastry Material " + currentPastryMat.pastry_material_id);
            return response;

        }

        //POST
        [HttpPost]
        public async Task<IActionResult> AddNewPastryMaterial(PostPastryMaterial newEntry)
        {
            byte[] designId = newEntry.design_id;
            try { Designs? selectedDesign = await _context.Designs.Where(x => x.isActive == true && x.design_id == designId).FirstAsync(); }
            catch (InvalidOperationException e) { return NotFound(new { message = "Design with the specified id not found" }); }

            foreach (PostIngredients entry in newEntry.ingredients)
            {

                switch (entry.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        //!!!UNTESTED!!!
                        Item? currentInventoryItemI = null;
                        try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(entry.item_id)).FirstAsync(); }
                        
                        catch (FormatException exF) { return BadRequest(new { message = "Invalid id format for " + entry.item_id + ", must be a value that can be parsed as an integer." }); }
                        catch (InvalidOperationException exO) { return NotFound(new { message = "The id " + entry.item_id + " does not exist in the inventory" }); }

                        if (currentInventoryItemI == null) { return NotFound(new { message = "Item " + entry.item_id + " is not found in the inventory." }); }
                        if (ValidUnits.IsSameQuantityUnit(currentInventoryItemI.measurements, entry.amount_measurement) == false) { return BadRequest(new { message = "Ingredient with the inventory item id " + currentInventoryItemI.id + " does not have the same quantity unit as the referred inventory item" }); }

                        break;
                    case IngredientType.Material:
                        //Check if item id exists on the 'Materials' table
                        //or in the inventory
                        Materials? currentReferredMaterial = null;
                        try { currentReferredMaterial = await _context.Materials.Where(x => x.isActive == true && x.material_id == entry.item_id).FirstAsync(); }
                        catch { return NotFound(new { message = "Id specified in the request does not exist in the database. Id " + entry.item_id }); }
                        //Add additional code here for inventory id checking
                        if (currentReferredMaterial == null) { return NotFound(new { message = "Id specified in the request does not exist in the database. Id " + entry.item_id }); }
                        if (ValidUnits.IsSameQuantityUnit(currentReferredMaterial.amount_measurement, entry.amount_measurement) == false) { return BadRequest(new { message = "Ingredient with the material item id " + currentReferredMaterial.material_id + " does not have the same quantity unit as the referred material" }); }
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
            newPastryMaterialEntry.design_id = designId;
            newPastryMaterialEntry.date_added = currentTime;
            newPastryMaterialEntry.last_modified_date = currentTime;
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

            foreach (PostIngredients entry in newEntry.ingredients)
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
                newIngredientsEntry.date_added = currentTime;
                newIngredientsEntry.last_modified_date = currentTime;

                await _context.Ingredients.AddAsync(newIngredientsEntry);
            }

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add Pastry Materials " + newPastryMaterialEntry.pastry_material_id);
            return Ok(new { message = "Data inserted to the database." });

        }
        [HttpPost("{pastry_material_id}/ingredients")]
        public async Task<IActionResult> AddNewPastryMaterialIngredient(string pastry_material_id, PostIngredients entry)
        {
            PastryMaterials? currentPastryMaterial = await _context.PastryMaterials.Where(x
                => x.isActive == true).FirstAsync(x => x.pastry_material_id == pastry_material_id);
            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }

            switch (entry.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    //!!!UNTESTED!!!
                    Item? currentInventoryItemI = null;
                    try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(entry.item_id)).FirstAsync(); }

                    catch (FormatException exF) { return BadRequest(new { message = "Invalid id format for " + entry.item_id + ", must be a value that can be parsed as an integer." }); }
                    catch (InvalidOperationException exO) { return NotFound(new { message = "The id " + entry.item_id + " does not exist in the inventory" }); }

                    if (currentInventoryItemI == null) { return NotFound(new { message = "Item " + entry.item_id + " is not found in the inventory." }); }
                    if (ValidUnits.IsSameQuantityUnit(currentInventoryItemI.measurements, entry.amount_measurement) == false) { return BadRequest(new { message = "Ingredient with the inventory item id " + currentInventoryItemI.id + " does not have the same quantity unit as the referred inventory item" }); }

                    break;
                case IngredientType.Material:
                    //Check if item id exists on the 'Materials' table
                    //or in the inventory
                    Materials? currentReferredMaterial = null;
                    try { currentReferredMaterial = await _context.Materials.Where(x => x.isActive == true && x.material_id == entry.item_id).FirstAsync(); }
                    catch { return NotFound(new { message = "Id specified in the request does not exist in the database. Id " + entry.item_id }); }
                    //Add additional code here for inventory id checking
                    if (currentReferredMaterial == null) { return NotFound(new { message = "Id specified in the request does not exist in the database. Id " + entry.item_id }); }
                    if (ValidUnits.IsSameQuantityUnit(currentReferredMaterial.amount_measurement, entry.amount_measurement) == false) { return BadRequest(new { message = "Ingredient with the material item id " + currentReferredMaterial.material_id + " does not have the same quantity unit as the referred material" }); }
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
            newIngredientsEntry.date_added = currentTime;
            newIngredientsEntry.last_modified_date = currentTime;

            await _context.Ingredients.AddAsync(newIngredientsEntry);
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add Ingredient " + newIngredientsEntry.ingredient_id + " to " + pastry_material_id);
            return Ok(new { message = "Data inserted to the database." });

        }

        //PATCH
        [HttpPatch("{pastry_material_id}")]
        public async Task<IActionResult> UpdatePastryMaterial(string pastry_material_id, PatchPastryMaterials entry)
        {

            byte[] designId = entry.design_id;
            try { Designs? selectedDesign = await _context.Designs.Where(x => x.isActive == true && x.design_id == designId).FirstAsync(); }
            catch (InvalidOperationException e) { return NotFound(new { message = "Design with the specified id not found" }); }

            PastryMaterials? currentPastryMaterial = null;
            try { currentPastryMaterial = await _context.PastryMaterials.Where(x => x.isActive == true).FirstAsync(x => x.pastry_material_id == pastry_material_id); }
            catch (InvalidOperationException exO) { return NotFound(new { message = "The pastry material with the id " + pastry_material_id + " does not exist." }); }
            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }

            DateTime currentTime = DateTime.Now;

            _context.PastryMaterials.Update(currentPastryMaterial);
            currentPastryMaterial.design_id = entry.design_id;
            currentPastryMaterial.last_modified_date = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update Pastry Material " + pastry_material_id);
            return Ok(new { message = "Pastry Material updated." });

        }
        [HttpPatch("{pastry_material_id}/{ingredient_id}")]
        public async Task<IActionResult> UpdatePastryMaterialIngredient(string pastry_material_id, string ingredient_id, PatchIngredients entry)
        {
            PastryMaterials? currentPastryMaterial = await _context.PastryMaterials.Where(x
                => x.isActive == true).FirstAsync(x => x.pastry_material_id == pastry_material_id);
            Ingredients? currentIngredient = await _context.Ingredients.Where(x => x.isActive == true).FirstAsync(x => x.ingredient_id == ingredient_id);

            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }
            if (currentIngredient == null) { return NotFound(new { message = "No Ingredient with the specified id found." }); }

            switch (entry.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    Item? currentInventoryItemI = null;
                    try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(entry.item_id)).FirstAsync(); }

                    catch (FormatException exF) { return BadRequest(new { message = "Invalid id format for " + entry.item_id + ", must be a value that can be parsed as an integer." }); }
                    catch (InvalidOperationException exO) { return NotFound(new { message = "The id " + entry.item_id + " does not exist in the inventory" }); }

                    if (currentInventoryItemI == null) { return NotFound(new { message = "Item " + entry.item_id + " is not found in the inventory." }); }
                    if (ValidUnits.IsSameQuantityUnit(currentInventoryItemI.measurements, entry.amount_measurement) == false) { return BadRequest(new { message = "Ingredient with the inventory item id " + currentInventoryItemI.id + " does not have the same quantity unit as the referred inventory item" }); }

                    break;
                case IngredientType.Material:
                    //Check if item id exists on the 'Materials' table
                    //or in the inventory
                    Materials? currentReferredMaterial = null;
                    try { currentReferredMaterial = await _context.Materials.Where(x => x.isActive == true && x.material_id == entry.item_id).FirstAsync(); }
                    catch { return NotFound(new { message = "Id specified in the request does not exist in the database. Id " + entry.item_id }); }
                    //Add additional code here for inventory id checking
                    if (currentReferredMaterial == null) { return NotFound(new { message = "Id specified in the request does not exist in the database. Id " + entry.item_id }); }
                    if (ValidUnits.IsSameQuantityUnit(currentReferredMaterial.amount_measurement, entry.amount_measurement) == false) { return BadRequest(new { message = "Ingredient with the material item id " + currentReferredMaterial.material_id + " does not have the same quantity unit as the referred material" }); }
                    break;
                default:
                    return BadRequest(new { message = "Ingredients to be inserted has an invalid ingredient_type, valid types are MAT and INV." });
            }

            DateTime currentTime = DateTime.Now;

            _context.Ingredients.Update(currentIngredient);
            currentIngredient.item_id = entry.item_id;
            currentIngredient.amount = entry.amount;
            currentIngredient.amount_measurement = entry.amount_measurement;
            currentIngredient.last_modified_date = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update Pastry Material Ingredient " + pastry_material_id);
            return Ok(new { message = "Pastry Material Ingredient updated." });

        }

        //DELETE
        [HttpDelete("{pastry_material_id}")]
        public async Task<IActionResult> DeletePastryMaterial(string pastry_material_id)
        {
            PastryMaterials? currentPastryMaterial = null;
            try { currentPastryMaterial = await _context.PastryMaterials.Where(x => x.pastry_material_id == pastry_material_id && x.isActive == true).FirstAsync(); }
            catch (InvalidOperationException exO) { return NotFound(new { message = "The pastry material with the id " + pastry_material_id + " does not exist" }); }

            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }

            DateTime currentTime = DateTime.Now;

            List<Ingredients> ingredients = await _context.Ingredients.Where(x => x.pastry_material_id == pastry_material_id && x.isActive == true).ToListAsync();

            foreach (Ingredients i in ingredients)
            {
                _context.Ingredients.Update(i);
                i.last_modified_date = currentTime;
                i.isActive = false;
            }
            _context.PastryMaterials.Update(currentPastryMaterial);
            currentPastryMaterial.isActive = false;
            currentPastryMaterial.last_modified_date = currentTime;

            await _context.SaveChangesAsync();
            await _actionLogger.LogAction(User, "DELETE", "Delete Pastry Material " + pastry_material_id);
            return Ok(new { message = "Pastry Material deleted." });
        }
        [HttpDelete("{pastry_material_id}/{ingredient_id}")]
        public async Task<IActionResult> DeletePastryMaterialIngredient(string pastry_material_id, string ingredient_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await _context.PastryMaterials.Where(x => x.isActive == true).FirstAsync(x => x.pastry_material_id == pastry_material_id); }
            catch (InvalidOperationException exO) { return NotFound(new { message = "The pastry material with the id " + pastry_material_id + " does not exist." }); }

            Ingredients? ingredientAboutToBeDeleted = await _context.Ingredients.FindAsync(ingredient_id);
            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }
            if (ingredientAboutToBeDeleted == null) { return NotFound(new { message = "Specified ingredient with the selected ingredient_id not found." }); }
            if (ingredientAboutToBeDeleted.isActive == false) { return NotFound(new { message = "Specified ingredient with the selected ingredient_id already deleted." }); }

            DateTime currentTime = DateTime.Now;
            _context.Ingredients.Update(ingredientAboutToBeDeleted);
            ingredientAboutToBeDeleted.last_modified_date = currentTime;
            ingredientAboutToBeDeleted.isActive = false;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Delete Pastry Material Ingredient " + ingredient_id);
            return Ok(new { message = "Ingredient deleted." });
        }

        //
        // INVENTORY ACTIONS
        //

        //!!!UNTESTED!!!
        [HttpPost("{pastry_material_id}/subtract_recipe_ingredients_on_inventory")]
        public async Task<IActionResult> SubtractPastryMaterialIngredientsOnInventory(string pastry_material_id)
        {
            PastryMaterials? currentPastryMaterial = await _context.PastryMaterials.FindAsync(pastry_material_id);
            List<Ingredients> currentPastryIngredients = await _context.Ingredients.Where(x => x.isActive == true && x.pastry_material_id == pastry_material_id).ToListAsync();

            if (currentPastryMaterial == null) { return NotFound(new { message = "No pastry material with the specified id found" }); }
            if (currentPastryIngredients.IsNullOrEmpty()) { return StatusCode(500, new { message = "The specified pastry material does not contain any active ingredients" }); }

            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients
            Dictionary<string, InventorySubtractorInfo> inventoryItemsAboutToBeSubtracted = new Dictionary<string, InventorySubtractorInfo>();

            foreach (Ingredients currentIngredient in currentPastryIngredients)
            {
                //Check if the measurement unit in the ingredient record is valid
                //If not found, skip current ingredient
                string? amountQuantityType = null;
                string? amountUnitMeasurement = null;

                bool isAmountMeasurementValid = false;
                foreach (string unitQuantity in validMeasurementUnits.Keys)
                {
                    List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                    string? currentMeasurement = currentQuantityUnits.Find(x => x.Equals(currentIngredient.amount_measurement));

                    if (currentMeasurement == null) { continue; }
                    else 
                    { 
                        isAmountMeasurementValid = true; 
                        amountQuantityType = unitQuantity; 
                        amountUnitMeasurement = currentMeasurement;
                    }
                }
                if (isAmountMeasurementValid == false) { return BadRequest(new { message = "The measurement of the pastry ingredient with the id " + currentIngredient.ingredient_id + " is not valid." }); }

                switch (currentIngredient.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        {
                            //Find the referred item
                            Item? currentRefInvItem = null;
                            try
                            { currentRefInvItem = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(currentIngredient.item_id)).FirstAsync(); }
                            catch (FormatException e) { return StatusCode(500, new { message = "The pastry ingredient with the type of " + IngredientType.InventoryItem + " and the ingredient id " + currentIngredient.ingredient_id + " cannot be parsed as an integer" }); }
                            catch (InvalidOperationException e) { return NotFound(new { message = "The pastry ingredient with the type of " + IngredientType.InventoryItem + " and the item id " + currentIngredient.item_id + " does not exist in the inventory" }); }

                            string currentItemMeasurement = currentIngredient.amount_measurement;
                            double currentItemAmount = currentIngredient.amount;

                            //Calculate the value to subtract here
                            InventorySubtractorInfo? inventorySubtractorInfoForCurrentIngredient = null;
                            inventoryItemsAboutToBeSubtracted.TryGetValue(currentIngredient.item_id, out inventorySubtractorInfoForCurrentIngredient);

                            if (inventorySubtractorInfoForCurrentIngredient == null)
                            {
                                inventoryItemsAboutToBeSubtracted.Add(currentIngredient.item_id, new InventorySubtractorInfo(amountQuantityType, amountUnitMeasurement, currentIngredient.amount));
                            }
                            else
                            {
                                if (inventorySubtractorInfoForCurrentIngredient.AmountUnit == currentItemMeasurement) { inventorySubtractorInfoForCurrentIngredient.Amount += currentItemAmount; }
                                else
                                {
                                    double amountInRecordedUnit = UnitConverter.ConvertByName(currentItemAmount, inventorySubtractorInfoForCurrentIngredient.AmountQuantityType, currentItemMeasurement, inventorySubtractorInfoForCurrentIngredient.AmountUnit);
                                    inventorySubtractorInfoForCurrentIngredient.Amount += amountInRecordedUnit;
                                }
                            }
                            break;
                        }
                    case IngredientType.Material:
                        {
                            List<MaterialIngredients> currentMaterialIngredients = await _context.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentIngredient.item_id).ToListAsync();

                            //This block loops thru the retrieved ingredients above
                            //And adds all sub-ingredients for the "MAT" type entries
                            int currentIndex = 0;
                            bool running = true;
                            while (running)
                            {
                                MaterialIngredients? currentMatIngInLoop = null;
                                try { currentMatIngInLoop = currentMaterialIngredients.ElementAt(currentIndex); }
                                catch { running = false; break; }

                                if (currentMatIngInLoop.ingredient_type == IngredientType.Material)
                                {
                                    List<MaterialIngredients> newEntriesToLoopThru = await _context.MaterialIngredients.Where(x => x.isActive == true && x.material_ingredient_id == currentMatIngInLoop.material_ingredient_id).ToListAsync();
                                    currentMaterialIngredients.AddRange(newEntriesToLoopThru);
                                }
                                currentIndex += 1;
                            }

                            //Removes all the entries for the material
                            //As the sub-ingredients for them is already in the list
                            currentMaterialIngredients.RemoveAll(x => x.ingredient_type == IngredientType.Material);

                            //Loop through the retrieved ingredients, then add them into the list of items to be subtracted in the inventory
                            foreach (MaterialIngredients currentMaterialIngredient in currentMaterialIngredients)
                            {
                                //Find the referred item
                                Item? currentRefInvItem = null;
                                try
                                { currentRefInvItem = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(currentMaterialIngredient.item_id)).FirstAsync(); }
                                catch (FormatException e) { return StatusCode(500, new { message = "The material ingredient for " + currentMaterialIngredient.material_id + " with the id " + currentMaterialIngredient.material_ingredient_id + ", failed to parse its item id " + currentMaterialIngredient.item_id + " as an integer" }); }
                                catch (InvalidOperationException e) { return NotFound(new { message = "The material ingredient for " + currentMaterialIngredient.material_id + " with the id " + currentMaterialIngredient.material_ingredient_id + ",  its item id " + currentMaterialIngredient.item_id + " does not refer to any active inventory record" }); }

                                string currentItemMeasurement = currentMaterialIngredient.amount_measurement;
                                double currentItemAmount = currentMaterialIngredient.amount;

                                //Calculate the value to subtract here
                                InventorySubtractorInfo? inventorySubtractorInfoForCurrentIngredient = null;
                                inventoryItemsAboutToBeSubtracted.TryGetValue(currentIngredient.item_id, out inventorySubtractorInfoForCurrentIngredient);
                                if (inventorySubtractorInfoForCurrentIngredient == null)
                                {
                                    inventoryItemsAboutToBeSubtracted.Add(currentIngredient.item_id, new InventorySubtractorInfo(amountQuantityType, amountUnitMeasurement, currentIngredient.amount));
                                }
                                else
                                {
                                    if (inventorySubtractorInfoForCurrentIngredient.AmountUnit == currentItemMeasurement) { inventorySubtractorInfoForCurrentIngredient.Amount += currentItemAmount; }
                                    else
                                    {
                                        double amountInRecordedUnit = UnitConverter.ConvertByName(currentItemAmount, inventorySubtractorInfoForCurrentIngredient.AmountQuantityType, currentItemMeasurement, inventorySubtractorInfoForCurrentIngredient.AmountUnit);
                                        inventorySubtractorInfoForCurrentIngredient.Amount += amountInRecordedUnit;
                                    }
                                }
                                break;
                            }
                            break;
                        }
                }
            }

            foreach (string currentInventoryItemId in inventoryItemsAboutToBeSubtracted.Keys)
            {
                InventorySubtractorInfo currentInventorySubtractorInfo = inventoryItemsAboutToBeSubtracted[currentInventoryItemId];

                //No need to check, record already checked earlier
                Item referencedInventoryItem = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(currentInventoryItemId)).FirstAsync();

                string? inventoryItemMeasurement = null;
                string? inventoryItemQuantityUnit = null;
                bool isInventoryItemMeasurementValid = false;
                //Add code here to check if the unit of the item in the inventory and the recorded total is the same
                foreach(string unitQuantity in validMeasurementUnits.Keys)
                {
                    List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                    string? currentMeasurement = currentQuantityUnits.Find(x => x.Equals(referencedInventoryItem.measurements));

                    if (currentMeasurement == null) { continue; }
                    else
                    {
                        isInventoryItemMeasurementValid = true;
                        inventoryItemQuantityUnit = unitQuantity;
                        inventoryItemMeasurement = currentMeasurement;
                    }
                }
                if (isInventoryItemMeasurementValid == false) { return StatusCode(500, new { message = "Inventory item with the id " + referencedInventoryItem.id + " measurement " + referencedInventoryItem.measurements + " is not valid" }); }
                if (inventoryItemQuantityUnit.Equals(currentInventorySubtractorInfo.AmountQuantityType) == false) { return StatusCode(500, new { message = "Inventory item with the id " + referencedInventoryItem.id + " measurement unit " + inventoryItemQuantityUnit + " does not match the quantity unit of one of the ingredients of the cake " + currentInventorySubtractorInfo.AmountQuantityType }); }

                if (inventoryItemQuantityUnit.Equals("Count")) { referencedInventoryItem.quantity = referencedInventoryItem.quantity - Convert.ToInt32(currentInventorySubtractorInfo.Amount); }
                else
                {
                    referencedInventoryItem.quantity = referencedInventoryItem.quantity - Convert.ToInt32(UnitConverter.ConvertByName(currentInventorySubtractorInfo.Amount, inventoryItemQuantityUnit, currentInventorySubtractorInfo.AmountUnit, referencedInventoryItem.measurements));
                }

                _kaizenTables.Item.Update(referencedInventoryItem);
            }

            await _kaizenTables.SaveChangesAsync();
            await _actionLogger.LogAction(User, "POST", "Subtract ingredients of " + pastry_material_id);
            return Ok(new { message = "Ingredients sucessfully deducted." });
        }
        private class InventorySubtractorInfo
        {

            public string AmountQuantityType;
            public string AmountUnit;
            public double Amount;

            public InventorySubtractorInfo() { }
            public InventorySubtractorInfo(string amountQuantityType, string amountUnit, double amount)
            {
                this.AmountQuantityType = amountQuantityType;
                this.AmountUnit = amountUnit;
                this.Amount = amount;
            }
        }

        private async Task<GetPastryMaterial> CreatePastryMaterialResponseFromDBRow(PastryMaterials data)
        {
            GetPastryMaterial response = new GetPastryMaterial();
            response.design_id = data.design_id;
            response.pastry_material_id = data.pastry_material_id;
            response.date_added = data.date_added;
            response.last_modified_date = data.last_modified_date;

            List<GetPastryMaterialIngredients> responsePastryMaterialList = new List<GetPastryMaterialIngredients>();
            double calculatedCost = 0.0;

            List<Ingredients> currentPastryMaterialIngredients = await _context.Ingredients.Where(x => x.isActive == true && x.pastry_material_id == data.pastry_material_id).ToListAsync();
            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients

            foreach (Ingredients currentIngredient in currentPastryMaterialIngredients)
            {
                GetPastryMaterialIngredients newSubIngredientListEntry = new GetPastryMaterialIngredients();

                //Check if the measurement unit in the ingredient record is valid
                //If not found, skip current ingredient
                string? amountQuantityType = null;
                string? amountUnitMeasurement = null;

                bool isAmountMeasurementValid = false;
                foreach (string unitQuantity in validMeasurementUnits.Keys)
                {
                    List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                    string? currentMeasurement = currentQuantityUnits.Find(x => x.Equals(currentIngredient.amount_measurement));

                    if (currentMeasurement == null) { continue; }
                    else
                    {
                        isAmountMeasurementValid = true;
                        amountQuantityType = unitQuantity;
                        amountUnitMeasurement = currentMeasurement;
                    }
                }
                if (isAmountMeasurementValid == false)
                {
                    throw new InvalidOperationException("The pastry material ingredient " + currentIngredient.ingredient_id + " has an invalid measurement unit"); //This should return something to identify the error
                }

                newSubIngredientListEntry.ingredient_id = currentIngredient.ingredient_id;
                newSubIngredientListEntry.pastry_material_id = currentIngredient.pastry_material_id;
                newSubIngredientListEntry.ingredient_type = currentIngredient.ingredient_type;

                newSubIngredientListEntry.amount = currentIngredient.amount;
                newSubIngredientListEntry.amount_measurement = currentIngredient.amount_measurement;

                newSubIngredientListEntry.date_added = currentIngredient.date_added;
                newSubIngredientListEntry.last_modified_date = currentIngredient.last_modified_date;

                switch (currentIngredient.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        {
                            //!!!UNTESTED!!!
                            Item? currentInventoryItemI = null;
                            try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(currentIngredient.item_id)).FirstAsync(); }
                            catch { continue; }
                            if (currentInventoryItemI == null) { continue; }

                            newSubIngredientListEntry.item_name = currentInventoryItemI.item_name;
                            newSubIngredientListEntry.item_id = Convert.ToString(currentInventoryItemI.id);

                            double convertedAmountI = 0.0;
                            double calculatedAmountI = 0.0;
                            if (amountQuantityType != "Count")
                            {
                                convertedAmountI = UnitConverter.ConvertByName(currentIngredient.amount, amountQuantityType, amountUnitMeasurement, currentInventoryItemI.measurements);
                                calculatedAmountI = convertedAmountI * currentInventoryItemI.price;
                            }
                            else
                            {
                                convertedAmountI = currentIngredient.amount;
                                calculatedAmountI = convertedAmountI * currentInventoryItemI.price;
                            }
                            calculatedCost += calculatedAmountI;
                            break;
                        }
                    case IngredientType.Material:
                        {
                            Materials? currentReferencedMaterial = await _context.Materials.Where(x => x.material_id == currentIngredient.item_id && x.isActive == true).FirstAsync();
                            if (currentReferencedMaterial == null) { continue; }

                            newSubIngredientListEntry.item_name = currentReferencedMaterial.material_name;
                            newSubIngredientListEntry.item_id = currentReferencedMaterial.material_id;

                            List<MaterialIngredients> currentMaterialReferencedIngredients = await _context.MaterialIngredients.Where(x => x.material_id == currentIngredient.item_id).ToListAsync();

                            if (!currentMaterialReferencedIngredients.IsNullOrEmpty())
                            {
                                List<SubGetMaterialIngredients> newEntryMaterialIngredients = new List<SubGetMaterialIngredients>();

                                foreach (MaterialIngredients materialIngredients in currentMaterialReferencedIngredients)
                                {
                                    SubGetMaterialIngredients newEntryMaterialIngredientsEntry = new SubGetMaterialIngredients();

                                    switch (materialIngredients.ingredient_type)
                                    {
                                        case IngredientType.InventoryItem:
                                            Item? currentSubMaterialReferencedInventoryItem = null;
                                            try { currentSubMaterialReferencedInventoryItem = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(materialIngredients.item_id)).FirstAsync(); }
                                            catch { continue; }
                                            if (currentSubMaterialReferencedInventoryItem == null) { continue; }
                                            else { newEntryMaterialIngredientsEntry.item_name = currentSubMaterialReferencedInventoryItem.item_name; }
                                            break;
                                        case IngredientType.Material:
                                            Materials? currentSubMaterialReferencedMaterial = await _context.Materials.Where(x => x.material_id == materialIngredients.item_id && x.isActive == true).FirstAsync();
                                            if (currentSubMaterialReferencedMaterial == null) { continue; }
                                            else { newEntryMaterialIngredientsEntry.item_name = currentSubMaterialReferencedMaterial.material_name; }
                                            break;
                                    }
                                    newEntryMaterialIngredientsEntry.material_id = materialIngredients.material_id;
                                    newEntryMaterialIngredientsEntry.material_ingredient_id = materialIngredients.material_ingredient_id;
                                    newEntryMaterialIngredientsEntry.item_id = materialIngredients.item_id;
                                    newEntryMaterialIngredientsEntry.ingredient_type = materialIngredients.ingredient_type;
                                    newEntryMaterialIngredientsEntry.amount = materialIngredients.amount;
                                    newEntryMaterialIngredientsEntry.amount_measurement = materialIngredients.amount_measurement;
                                    newEntryMaterialIngredientsEntry.date_added = materialIngredients.date_added;
                                    newEntryMaterialIngredientsEntry.last_modified_date = materialIngredients.last_modified_date;

                                    newEntryMaterialIngredients.Add(newEntryMaterialIngredientsEntry);
                                }
                                newSubIngredientListEntry.material_ingredients = newEntryMaterialIngredients;
                            }
                            else
                            {
                                newSubIngredientListEntry.material_ingredients = new List<SubGetMaterialIngredients>();
                            }
                            //Price calculation code
                            //Get all ingredient for currently referenced material
                            List<MaterialIngredients> subIngredientsForCurrentIngredient = currentMaterialReferencedIngredients.Where(x => x.ingredient_type == IngredientType.InventoryItem).ToList();
                            double currentSubIngredientCostMultiplier = amountUnitMeasurement.Equals(currentReferencedMaterial.amount_measurement) ? currentReferencedMaterial.amount / currentIngredient.amount : currentReferencedMaterial.amount / UnitConverter.ConvertByName(currentIngredient.amount, amountQuantityType, amountUnitMeasurement, currentReferencedMaterial.amount_measurement);
                            foreach (MaterialIngredients subIng in subIngredientsForCurrentIngredient)
                            {
                                Item? currentReferencedIngredientM = null;
                                try { currentReferencedIngredientM = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(subIng.item_id)).FirstAsync(); }
                                catch (Exception e) { Console.WriteLine("Error in retrieving " + subIng.item_id + " on inventory: " + e.GetType().ToString()); continue; }

                                double currentRefItemPrice = currentReferencedIngredientM.price;
                                double ingredientCost = currentReferencedIngredientM.measurements == subIng.amount_measurement ? (currentRefItemPrice * currentIngredient.amount) * currentSubIngredientCostMultiplier : (currentRefItemPrice * UnitConverter.ConvertByName(currentIngredient.amount, amountQuantityType, amountUnitMeasurement, currentReferencedIngredientM.measurements) * currentSubIngredientCostMultiplier);

                                calculatedCost += ingredientCost;
                            }

                            //Get All material types of ingredient of the current ingredient
                            List<MaterialIngredients> subMaterials = currentMaterialReferencedIngredients.Where(x => x.ingredient_type == IngredientType.Material).ToList();
                            int subMaterialIngLoopIndex = 0;
                            bool isLoopingThroughSubMaterials = true;

                            while (isLoopingThroughSubMaterials)
                            {
                                MaterialIngredients currentSubMaterial;
                                try { currentSubMaterial = subMaterials[subMaterialIngLoopIndex]; }
                                catch (Exception e) { isLoopingThroughSubMaterials = false; break; }

                                Materials currentReferencedMaterialForSub = await _context.Materials.Where(x => x.isActive == true && x.material_id == currentSubMaterial.item_id).FirstAsync();

                                string refMatMeasurement = currentReferencedMaterialForSub.amount_measurement;
                                double refMatAmount = currentReferencedMaterialForSub.amount;

                                string subMatMeasurement = currentSubMaterial.amount_measurement;
                                double subMatAmount = currentSubMaterial.amount;

                                string measurementQuantity = "";

                                foreach (string unitQuantity in validMeasurementUnits.Keys)
                                {
                                    List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                                    string? currentSubMatMeasurement = currentQuantityUnits.Find(x => x.Equals(subMatMeasurement));
                                    string? currentRefMatMeasurement = currentQuantityUnits.Find(x => x.Equals(refMatMeasurement));

                                    if (currentSubMatMeasurement != null && currentRefMatMeasurement != null) { measurementQuantity = unitQuantity; }
                                    else { continue; }
                                }

                                double costMultiplier = refMatMeasurement == subMatMeasurement ? refMatAmount / subMatAmount : refMatAmount / UnitConverter.ConvertByName(subMatAmount, measurementQuantity, subMatMeasurement, refMatMeasurement);

                                List<MaterialIngredients> subMaterialIngredients = await _context.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentReferencedMaterialForSub.material_id).ToListAsync();
                                foreach (MaterialIngredients subMaterialIngredientsRow in subMaterialIngredients)
                                {
                                    switch (subMaterialIngredientsRow.ingredient_type)
                                    {
                                        case IngredientType.InventoryItem:
                                            Item? refItemForSubMatIng = null;
                                            try { refItemForSubMatIng = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(subMaterialIngredientsRow.item_id)).FirstAsync(); }
                                            catch (Exception e) { Console.WriteLine("Error in retrieving " + subMaterialIngredientsRow.item_id + " on inventory: " + e.GetType().ToString()); continue; }

                                            string subMatIngRowMeasurement = subMaterialIngredientsRow.amount_measurement;
                                            double subMatIngRowAmount = subMaterialIngredientsRow.amount;

                                            string refItemMeasurement = refItemForSubMatIng.measurements;
                                            double refItemPrice = refItemForSubMatIng.price;

                                            string refItemQuantityUnit = "";
                                            foreach (string unitQuantity in validMeasurementUnits.Keys)
                                            {
                                                List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                                                string? currentSubMatMeasurement = currentQuantityUnits.Find(x => x.Equals(subMatIngRowMeasurement));
                                                string? currentRefMatMeasurement = currentQuantityUnits.Find(x => x.Equals(refItemMeasurement));

                                                if (currentSubMatMeasurement != null && currentRefMatMeasurement != null) { refItemQuantityUnit = unitQuantity; }
                                                else { continue; }
                                            }

                                            double currentSubMaterialIngredientPrice = refItemForSubMatIng.measurements == subMaterialIngredientsRow.amount_measurement ? (refItemPrice * subMatIngRowAmount) * costMultiplier : (refItemPrice * UnitConverter.ConvertByName(subMatIngRowAmount, refItemQuantityUnit, subMatIngRowMeasurement, refItemMeasurement)) * costMultiplier;

                                            calculatedCost += currentSubMaterialIngredientPrice;
                                            break;
                                        case IngredientType.Material:
                                            subMaterials.Add(subMaterialIngredientsRow);
                                            break;
                                    }
                                }
                                subMaterialIngLoopIndex += 1;

                                break;
                            }
                            break;
                        }

                }
                responsePastryMaterialList.Add(newSubIngredientListEntry);
            }
            response.ingredients = responsePastryMaterialList;
            response.cost_estimate = calculatedCost;
            return response;
        }

    }
}
