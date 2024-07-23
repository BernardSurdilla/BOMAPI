using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Helpers;

using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Text;
using UnitsNet;
using BOM_API_v2.Services;
using System.Runtime.CompilerServices;
using Castle.Components.DictionaryAdapter.Xml;
using System.Text.Json;
using Microsoft.Identity.Client;
using ZstdSharp.Unsafe;

namespace BOM_API_v2.Controllers
{
    [ApiController]
    [Route("pastry-materials")]
    [Authorize]
    public class PastryMaterialController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        private readonly IActionLogger _actionLogger;

        public PastryMaterialController(DatabaseContext context, KaizenTables kaizen, IActionLogger logs, ICakePriceCalculator cakePriceCalculator) { _context = context; _actionLogger = logs; _kaizenTables = kaizen; }

        //GET
        [HttpGet]
        [Authorize(Roles = UserRoles.Admin)]
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
                try { newRow = await DataParser.CreatePastryMaterialResponseFromDBRow(currentPastryMaterial, _context, _kaizenTables); }
                catch { continue; }
                response.Add(newRow);
            }

            await _actionLogger.LogAction(User, "GET", "All Pastry Material ");
            return response;
        }
        [HttpGet("{pastry_material_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<GetPastryMaterial> GetSpecificPastryMaterial(string pastry_material_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch { return new GetPastryMaterial(); }

            List<Ingredients> ingredientsForCurrentMaterial = await _context.Ingredients.Where(x => x.isActive == true && x.pastry_material_id == currentPastryMaterial.pastry_material_id).ToListAsync();
            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients

            GetPastryMaterial response;
            try { response = await DataParser.CreatePastryMaterialResponseFromDBRow(currentPastryMaterial, _context, _kaizenTables); }
            catch (InvalidOperationException e) { return new GetPastryMaterial(); }

            await _actionLogger.LogAction(User, "GET", "Pastry Material " + currentPastryMaterial.pastry_material_id);
            return response;

        }
        [HttpGet("{pastry_material_id}/ingredients")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<List<GetPastryMaterialIngredients>> GetAllPastryMaterialIngredient(string pastry_material_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch { return new List<GetPastryMaterialIngredients>(); }

            List<Ingredients> currentIngredient = await _context.Ingredients.Where(x => x.pastry_material_id == pastry_material_id && x.isActive == true).ToListAsync();
            List<GetPastryMaterialIngredients> response = new List<GetPastryMaterialIngredients>();

            if (currentIngredient == null || currentPastryMaterial == null) { return response; }

            GetPastryMaterial encodedDbRow;
            try { encodedDbRow = await DataParser.CreatePastryMaterialResponseFromDBRow(currentPastryMaterial, _context, _kaizenTables); }
            catch { return response; }
            response = encodedDbRow.ingredients;

            await _actionLogger.LogAction(User, "GET", "All Pastry Material Ingredients");
            return response;
        }

        //POST
        [HttpPost]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewPastryMaterial(PostPastryMaterial newEntry)
        {
            if (await DataVerification.DesignExistsAsync(newEntry.design_id, _context) == false) { return NotFound(new {message = "No design with the id " + newEntry.design_id + " found."}); }
            foreach (PostIngredients entry in newEntry.ingredients)
            {
                switch (entry.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        //!!!UNTESTED!!!
                        Item? currentInventoryItemI = null;
                        try { currentInventoryItemI = await DataRetrieval.GetInventoryItemAsync(entry.item_id, _kaizenTables); }
                        
                        catch (FormatException exF) { return BadRequest(new { message = exF.Message }); }
                        catch (NotFoundInDatabaseException exO) { return NotFound(new { message = exO.Message }); }

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
            if (newEntry.add_ons.IsNullOrEmpty() == false)
            {
                foreach (PostPastryMaterialAddOns addOnEntry in newEntry.add_ons)
                {
                    AddOns? selectedAddOn = null;
                    try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(addOnEntry.add_ons_id, _kaizenTables); }
                    catch(Exception e) { return NotFound(new { message = e.Message }); }
                }
            }
            if (newEntry.sub_variants != null)
            {
                foreach (PostPastryMaterialSubVariant entry_sub_variant in newEntry.sub_variants)
                {
                    if (newEntry.main_variant_name.Equals(entry_sub_variant.sub_variant_name)) { return BadRequest(new { message = "Sub variants cannot have the same name as the main variant" }); }
                    foreach (PostPastryMaterialSubVariantIngredients entry_sub_variant_ingredients in entry_sub_variant.sub_variant_ingredients)
                    {
                        switch (entry_sub_variant_ingredients.ingredient_type)
                        {
                            case IngredientType.InventoryItem:
                                //!!!UNTESTED!!!
                                Item? currentInventoryItemI = null;
                                try { currentInventoryItemI = await DataRetrieval.GetInventoryItemAsync(entry_sub_variant_ingredients.item_id, _kaizenTables); }
                                catch (FormatException exF) { return BadRequest(new { message = exF.Message }); }
                                catch (NotFoundInDatabaseException exO) { return NotFound(new { message = exO.Message }); }

                                if (ValidUnits.IsSameQuantityUnit(currentInventoryItemI.measurements, entry_sub_variant_ingredients.amount_measurement) == false) { return BadRequest(new { message = "Ingredient with the inventory item id " + currentInventoryItemI.id + " does not have the same quantity unit as the referred inventory item" }); }
                                break;
                            case IngredientType.Material:
                                //Check if item id exists on the 'Materials' table
                                //or in the inventory
                                Materials? currentReferredMaterial = null;
                                try { currentReferredMaterial = await _context.Materials.Where(x => x.isActive == true && x.material_id == entry_sub_variant_ingredients.item_id).FirstAsync(); }
                                catch { return NotFound(new { message = "Id specified in the request does not exist in the database. Id " + entry_sub_variant_ingredients.item_id }); }
                                //Add additional code here for inventory id checking
                                if (currentReferredMaterial == null) { return NotFound(new { message = "Id specified in the request does not exist in the database. Id " + entry_sub_variant_ingredients.item_id }); }
                                if (ValidUnits.IsSameQuantityUnit(currentReferredMaterial.amount_measurement, entry_sub_variant_ingredients.amount_measurement) == false) { return BadRequest(new { message = "Ingredient with the material item id " + currentReferredMaterial.material_id + " does not have the same quantity unit as the referred material" }); }
                                break;
                            default:
                                return BadRequest(new { message = "Ingredients to be inserted has an invalid ingredient_type, valid types are MAT and INV." });
                        }
                    }
                    if (entry_sub_variant.sub_variant_add_ons != null)
                    {
                        foreach (PostPastryMaterialSubVariantAddOns entry_sub_variant_add_on in entry_sub_variant.sub_variant_add_ons)
                        {
                            AddOns? selectedAddOn = null;
                            try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(entry_sub_variant_add_on.add_ons_id, _kaizenTables); }
                            catch(Exception e) { return NotFound(new { message = e.Message }); }
                        }
                    }
                }
            }

            PastryMaterials newPastryMaterialEntry = new PastryMaterials();

            DateTime currentTime = DateTime.Now;
            string newPastryMaterialId = await IdFormat.GetNewestPastryMaterialId(_context);

            newPastryMaterialEntry.pastry_material_id = newPastryMaterialId;
            newPastryMaterialEntry.main_variant_name = newEntry.main_variant_name;
            newPastryMaterialEntry.design_id = newEntry.design_id;
            newPastryMaterialEntry.date_added = currentTime;
            newPastryMaterialEntry.last_modified_date = currentTime;
            newPastryMaterialEntry.isActive = true;

            await _context.PastryMaterials.AddAsync(newPastryMaterialEntry);
            await _context.SaveChangesAsync();

            string lastPastryMaterialAddOnId = await IdFormat.GetNewestPastryMaterialAddOnId(_context);

            await DataInsertion.AddPastryMaterialIngredient(newPastryMaterialId, newEntry.ingredients, _context);
            if (newEntry.add_ons.IsNullOrEmpty() == false) await DataInsertion.AddPastryMaterialAddOns(newPastryMaterialId, newEntry.add_ons, _context);

            await _context.SaveChangesAsync();

            if (newEntry.sub_variants != null)
            {
                foreach (PostPastryMaterialSubVariant entry_sub_variant in newEntry.sub_variants)
                {
                    string lastPastryMaterialSubVariantId = await IdFormat.GetNewestPastryMaterialSubVariantId(_context);

                    PastryMaterialSubVariants newSubMaterialDbEntry = new PastryMaterialSubVariants();
                    newSubMaterialDbEntry.pastry_material_sub_variant_id = lastPastryMaterialSubVariantId;
                    newSubMaterialDbEntry.pastry_material_id = newPastryMaterialId;
                    newSubMaterialDbEntry.sub_variant_name = entry_sub_variant.sub_variant_name;
                    newSubMaterialDbEntry.date_added = currentTime;
                    newSubMaterialDbEntry.last_modified_date = currentTime;
                    newSubMaterialDbEntry.isActive = true;

                    await _context.PastryMaterialSubVariants.AddAsync(newSubMaterialDbEntry);
                    await _context.SaveChangesAsync();

                    await DataInsertion.AddPastryMaterialSubVariantIngredient(lastPastryMaterialSubVariantId, entry_sub_variant.sub_variant_ingredients, _context);
                    if (entry_sub_variant.sub_variant_add_ons.IsNullOrEmpty() == false) await DataInsertion.AddPastryMaterialSubVariantAddOn(lastPastryMaterialSubVariantId, entry_sub_variant.sub_variant_add_ons, _context);
                    
                }
            }
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add Pastry Materials " + newPastryMaterialEntry.pastry_material_id);
            return Ok(new { message = "Data inserted to the database." });

        }
        [HttpPost("{pastry_material_id}/ingredients")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewPastryMaterialIngredient(string pastry_material_id, PostIngredients entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            switch (entry.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    //!!!UNTESTED!!!
                    Item? currentInventoryItemI = null;
                    try { currentInventoryItemI = await DataRetrieval.GetInventoryItemAsync(entry.item_id, _kaizenTables); }
                    catch (FormatException exF) { return BadRequest(new { message = exF.Message }); }
                    catch (NotFoundInDatabaseException exO) { return NotFound(new { message = exO.Message }); }

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

            string newIngredientId = await DataInsertion.AddPastryMaterialIngredient(currentPastryMaterial.pastry_material_id, entry, _context);
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add Ingredient " + newIngredientId + " to " + pastry_material_id);
            return Ok(new { message = "Data inserted to the database." });

        }
        [HttpPost("{pastry_material_id}/add_ons")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewPastryMaterialAddOn(string pastry_material_id, PostPastryMaterialAddOns entry)
        {
            PastryMaterials? currentPastryMaterial = null;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            AddOns? selectedAddOn = null;
            try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(entry.add_ons_id, _kaizenTables); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            string newAddOnId = await DataInsertion.AddPastryMaterialAddOns(pastry_material_id, entry, _context);
            await _context.SaveChangesAsync();

            return Ok(new { message = "New add on added to " + pastry_material_id });

        }
        [HttpPost("{pastry_material_id}/sub-variants")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewPastryMaterialSubVariant(string pastry_material_id, PostPastryMaterialSubVariant entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }
            if (currentPastryMaterial.main_variant_name.Equals(entry.sub_variant_name)) { return BadRequest(new { message = "Sub variants cannot have the same name as the main variant" }); }

            //Ingredient Verification
            foreach (PostPastryMaterialSubVariantIngredients subVariantIngredient in entry.sub_variant_ingredients)
            {
                switch (subVariantIngredient.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        //!!!UNTESTED!!!
                        Item? currentInventoryItemI = null;
                        try { currentInventoryItemI = await DataRetrieval.GetInventoryItemAsync(subVariantIngredient.item_id, _kaizenTables); }
                        catch (FormatException exF) { return BadRequest(new { message = exF.Message }); }
                        catch (NotFoundInDatabaseException exO) { return NotFound(new { message = exO.Message }); }

                        if (currentInventoryItemI == null) { return NotFound(new { message = "Item " + subVariantIngredient.item_id + " is not found in the inventory." }); }
                        if (ValidUnits.IsSameQuantityUnit(currentInventoryItemI.measurements, subVariantIngredient.amount_measurement) == false) { return BadRequest(new { message = "Ingredient with the inventory item id " + currentInventoryItemI.id + " does not have the same quantity unit as the referred inventory item" }); }

                        break;
                    case IngredientType.Material:
                        //Check if item id exists on the 'Materials' table
                        //or in the inventory
                        Materials? currentReferredMaterial = null;
                        try { currentReferredMaterial = await _context.Materials.Where(x => x.isActive == true && x.material_id == subVariantIngredient.item_id).FirstAsync(); }
                        catch { return NotFound(new { message = "Id specified in the request does not exist in the database. Id " + subVariantIngredient.item_id }); }
                        //Add additional code here for inventory id checking
                        if (currentReferredMaterial == null) { return NotFound(new { message = "Id specified in the request does not exist in the database. Id " + subVariantIngredient.item_id }); }
                        if (ValidUnits.IsSameQuantityUnit(currentReferredMaterial.amount_measurement, subVariantIngredient.amount_measurement) == false) { return BadRequest(new { message = "Ingredient with the material item id " + currentReferredMaterial.material_id + " does not have the same quantity unit as the referred material" }); }
                        break;
                    default:
                        return BadRequest(new { message = "Ingredients to be inserted has an invalid ingredient_type, valid types are MAT and INV." });
                }
            }
            //Add on verification
            if (entry.sub_variant_add_ons != null)
            {
                foreach(PostPastryMaterialSubVariantAddOns subVariantAddOn in entry.sub_variant_add_ons)
                {
                    AddOns? selectedAddOn = null;
                    try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(subVariantAddOn.add_ons_id, _kaizenTables); }
                    catch (Exception e) { return NotFound(new { message = e.Message }); }
                }
            }

            DateTime currentTime = DateTime.Now;
            string lastPastryMaterialSubVariantId = await IdFormat.GetNewestPastryMaterialSubVariantId(_context);

            PastryMaterialSubVariants newSubMaterialDbEntry = new PastryMaterialSubVariants();
            newSubMaterialDbEntry.pastry_material_sub_variant_id = lastPastryMaterialSubVariantId;
            newSubMaterialDbEntry.pastry_material_id = pastry_material_id;
            newSubMaterialDbEntry.sub_variant_name = entry.sub_variant_name;
            newSubMaterialDbEntry.date_added = currentTime;
            newSubMaterialDbEntry.last_modified_date = currentTime;
            newSubMaterialDbEntry.isActive = true;

            await _context.PastryMaterialSubVariants.AddAsync(newSubMaterialDbEntry);
            await _context.SaveChangesAsync();

            await DataInsertion.AddPastryMaterialSubVariantIngredient(lastPastryMaterialSubVariantId, entry.sub_variant_ingredients, _context);
            if (entry.sub_variant_add_ons.IsNullOrEmpty() == false) { await DataInsertion.AddPastryMaterialSubVariantAddOn(lastPastryMaterialSubVariantId, entry.sub_variant_add_ons, _context); }

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add Sub variant " + lastPastryMaterialSubVariantId + " for " + pastry_material_id);
            return Ok(new { message = "New sub variant for " + pastry_material_id + " added" });
        }
        [Authorize(Roles = UserRoles.Admin)]
        [HttpPost("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/ingredients")]
        public async Task<IActionResult> AddNewPastryMaterialSubVariantIngredient(string pastry_material_id, string pastry_material_sub_variant_id, PostPastryMaterialSubVariantIngredients entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(currentPastryMaterial.pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            switch (entry.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    //!!!UNTESTED!!!
                    Item? currentInventoryItemI = null;
                    try { currentInventoryItemI = await DataRetrieval.GetInventoryItemAsync(entry.item_id, _kaizenTables); }

                    catch (FormatException exF) { return BadRequest(new { message = exF.Message }); }
                    catch (NotFoundInDatabaseException exO) { return NotFound(new { message = exO.Message }); }

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

            await DataInsertion.AddPastryMaterialSubVariantIngredient(currentPastryMaterialSubVariant.pastry_material_sub_variant_id, entry, _context);
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add Sub variant ingredient for " + pastry_material_sub_variant_id + " of " + pastry_material_id);
            return Ok(new { message = "New sub variant ingredient for " + pastry_material_sub_variant_id + " added" });
        }
        [HttpPost("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/add_ons")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewPastryMaterialSubVariantAddOn(string pastry_material_id, string pastry_material_sub_variant_id, PostPastryMaterialSubVariantAddOns entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(currentPastryMaterial.pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            AddOns? selectedAddOn = null;
            try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(entry.add_ons_id, _kaizenTables); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            await DataInsertion.AddPastryMaterialSubVariantAddOn(currentPastryMaterialSubVariant.pastry_material_sub_variant_id, entry, _context);
            await _context.SaveChangesAsync();

            return Ok(new { message = "New add on inserted to " + pastry_material_sub_variant_id });
        }

        //PATCH
        [HttpPatch("{pastry_material_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdatePastryMaterial(string pastry_material_id, PatchPastryMaterials entry)
        {
            if (await DataVerification.DesignExistsAsync(entry.design_id, _context) == false) { return NotFound(new { message = "No design with the id " + entry.design_id + " found." }); }

            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            DateTime currentTime = DateTime.Now;

            _context.PastryMaterials.Update(currentPastryMaterial);
            currentPastryMaterial.design_id = entry.design_id;
            currentPastryMaterial.main_variant_name = entry.main_variant_name;
            currentPastryMaterial.last_modified_date = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update Pastry Material " + pastry_material_id);
            return Ok(new { message = "Pastry Material updated." });

        }
        [HttpPatch("{pastry_material_id}/ingredients/{ingredient_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdatePastryMaterialIngredient(string pastry_material_id, string ingredient_id, PatchIngredients entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            Ingredients? currentIngredient;
            try { currentIngredient = await DataRetrieval.GetPastryMaterialIngredientAsync(pastry_material_id, ingredient_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            switch (entry.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    Item? currentInventoryItemI = null;
                    try { currentInventoryItemI = await DataRetrieval.GetInventoryItemAsync(entry.item_id, _kaizenTables); }

                    catch (FormatException exF) { return BadRequest(new { message = exF.Message }); }
                    catch (NotFoundInDatabaseException exO) { return NotFound(new { message = exO.Message }); }

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
        [HttpPatch("{pastry_material_id}/add_ons/{pastry_material_add_on_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdatePastryMaterialAddOn(string pastry_material_id, string pastry_material_add_on_id, PatchPastryMaterialAddOn entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialAddOns? currentPastryMaterialAddOn = null;
            try { currentPastryMaterialAddOn = await DataRetrieval.GetPastryMaterialAddOnAsync(pastry_material_id, pastry_material_add_on_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            AddOns? selectedAddOn = null;
            try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(entry.add_ons_id, _kaizenTables); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            _context.PastryMaterialAddOns.Update(currentPastryMaterialAddOn);
            currentPastryMaterialAddOn.add_ons_id = selectedAddOn.add_ons_id;
            currentPastryMaterialAddOn.amount = entry.amount;
            currentPastryMaterialAddOn.last_modified_date = DateTime.Now;

            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Add on " + pastry_material_add_on_id + " updated " });
        }
        [HttpPatch("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdatePastryMaterialSubVariant(string pastry_material_id, string pastry_material_sub_variant_id, PatchPastryMaterialSubVariants entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }
            if (currentPastryMaterial.main_variant_name.Equals(entry.sub_variant_name)) { return BadRequest(new { message = "Sub variants cannot have the same name as the main variant" }); }

            _context.PastryMaterialSubVariants.Update(currentPastryMaterialSubVariant);
            currentPastryMaterialSubVariant.last_modified_date = DateTime.Now;
            currentPastryMaterialSubVariant.sub_variant_name = entry.sub_variant_name;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update sub variant " + pastry_material_sub_variant_id);
            return Ok(new { message = "Sub variant updated" });
        }
        [HttpPatch("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/ingredients/{pastry_material_sub_variant_ingredient_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdatePastryMaterialSubVariantIngredient(string pastry_material_id, string pastry_material_sub_variant_id, string pastry_material_sub_variant_ingredient_id, PatchPastryMaterialSubVariantsIngredient entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariantIngredients? currentPastryMaterialSubVariantIngredient = null;
            try { currentPastryMaterialSubVariantIngredient = await DataRetrieval.GetPastryMaterialSubVariantIngredientAsync(pastry_material_id, pastry_material_sub_variant_id, pastry_material_sub_variant_ingredient_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            switch (entry.ingredient_type)
            {
                case IngredientType.InventoryItem:
                    Item? currentInventoryItemI = null;
                    try { currentInventoryItemI = await DataRetrieval.GetInventoryItemAsync(entry.item_id, _kaizenTables); }

                    catch (FormatException exF) { return BadRequest(new { message = exF.Message }); }
                    catch (NotFoundInDatabaseException exO) { return NotFound(new { message = exO.Message }); }

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

            _context.PastryMaterialSubVariantIngredients.Update(currentPastryMaterialSubVariantIngredient);
            currentPastryMaterialSubVariantIngredient.item_id = entry.item_id;
            currentPastryMaterialSubVariantIngredient.ingredient_type = entry.ingredient_type;
            currentPastryMaterialSubVariantIngredient.amount = entry.amount;
            currentPastryMaterialSubVariantIngredient.amount_measurement = entry.amount_measurement;
            currentPastryMaterialSubVariantIngredient.last_modified_date = DateTime.Now;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update sub variant " + pastry_material_sub_variant_id + " ingredient " + pastry_material_sub_variant_ingredient_id);
            return Ok(new { message = "Sub variant updated" });
        }
        [HttpPatch("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/add_ons/{pastry_material_sub_variant_add_on_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdatePastryMaterialSubVariantAddOn(string pastry_material_id, string pastry_material_sub_variant_id, string pastry_material_sub_variant_add_on_id, PatchPastryMaterialSubVariantAddOn entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariantAddOns? currentPastryMaterialSubVariantAddOn = null;
            try { currentPastryMaterialSubVariantAddOn = await DataRetrieval.GetPastryMaterialSubVariantAddOnAsync(pastry_material_id, pastry_material_sub_variant_id, pastry_material_sub_variant_add_on_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            AddOns? selectedAddOn = null;
            try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(entry.add_ons_id, _kaizenTables); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            _context.PastryMaterialSubVariantAddOns.Update(currentPastryMaterialSubVariantAddOn);
            currentPastryMaterialSubVariantAddOn.add_ons_id = selectedAddOn.add_ons_id;
            currentPastryMaterialSubVariantAddOn.amount = entry.amount;
            currentPastryMaterialSubVariantAddOn.last_modified_date = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Add on " + pastry_material_sub_variant_add_on_id + " updated " });
        }


        //DELETE
        [HttpDelete("{pastry_material_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeletePastryMaterial(string pastry_material_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

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
        [HttpDelete("{pastry_material_id}/ingredients/{ingredient_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeletePastryMaterialIngredient(string pastry_material_id, string ingredient_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            Ingredients? ingredientAboutToBeDeleted;
            try { ingredientAboutToBeDeleted = await DataRetrieval.GetPastryMaterialIngredientAsync(pastry_material_id, ingredient_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            DateTime currentTime = DateTime.Now;
            _context.Ingredients.Update(ingredientAboutToBeDeleted);
            ingredientAboutToBeDeleted.last_modified_date = currentTime;
            ingredientAboutToBeDeleted.isActive = false;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Delete Pastry Material Ingredient " + ingredient_id);
            return Ok(new { message = "Ingredient deleted." });
        }
        [HttpDelete("{pastry_material_id}/add_ons/{pastry_material_add_on_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeletePastryMaterialAddOn(string pastry_material_id, string pastry_material_add_on_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialAddOns? currentPastryMaterialAddOn = null;
            try { currentPastryMaterialAddOn = await DataRetrieval.GetPastryMaterialAddOnAsync(pastry_material_id, pastry_material_add_on_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            _context.PastryMaterialAddOns.Update(currentPastryMaterialAddOn);
            currentPastryMaterialAddOn.isActive = false;
            currentPastryMaterialAddOn.last_modified_date = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Add on " + pastry_material_add_on_id + " deleted " });
        }
        [HttpDelete("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeletePastryMaterialVariant(string pastry_material_id, string pastry_material_sub_variant_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            _context.PastryMaterialSubVariants.Update(currentPastryMaterialSubVariant);
            currentPastryMaterialSubVariant.isActive = false;
            currentPastryMaterialSubVariant.last_modified_date = DateTime.Now;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Delete sub variant " + pastry_material_sub_variant_id);
            return Ok(new { message = "Sub variant deleted" });
        }
        [HttpDelete("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/ingredients/{pastry_material_sub_variant_ingredient_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeletePastryMaterialVariantIngredient(string pastry_material_id, string pastry_material_sub_variant_id, string pastry_material_sub_variant_ingredient_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariantIngredients? currentPastryMaterialSubVariantIngredient = null;
            try { currentPastryMaterialSubVariantIngredient = await DataRetrieval.GetPastryMaterialSubVariantIngredientAsync(pastry_material_id, pastry_material_sub_variant_id, pastry_material_sub_variant_ingredient_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            _context.PastryMaterialSubVariantIngredients.Update(currentPastryMaterialSubVariantIngredient);
            currentPastryMaterialSubVariantIngredient.isActive = false;
            currentPastryMaterialSubVariantIngredient.last_modified_date = DateTime.Now;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Delete sub variant " + pastry_material_sub_variant_id + " ingredient " + pastry_material_sub_variant_ingredient_id);
            return Ok(new { message = "Sub variant deleted" });
        }
        [HttpDelete("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/add_ons/{pastry_material_sub_variant_add_on_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeletePastryMaterialSubVariantAddOn(string pastry_material_id, string pastry_material_sub_variant_id, string pastry_material_sub_variant_add_on_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariantAddOns? currentPastryMaterialSubVariantAddOn = null;
            try { currentPastryMaterialSubVariantAddOn = await DataRetrieval.GetPastryMaterialSubVariantAddOnAsync(pastry_material_id, pastry_material_sub_variant_id, pastry_material_sub_variant_add_on_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            _context.PastryMaterialSubVariantAddOns.Update(currentPastryMaterialSubVariantAddOn);
            currentPastryMaterialSubVariantAddOn.isActive = false;
            currentPastryMaterialSubVariantAddOn.last_modified_date = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Add on " + pastry_material_sub_variant_add_on_id + " deleted " });
        }

    }
}
