using BillOfMaterialsAPI.Helpers;
using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BOM_API_v2.Services;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BOM_API_v2.Controllers
{
    [ApiController]
    [Route("pastry-materials")]
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
            IQueryable<PastryMaterials> pastryMaterialQuery = _context.PastryMaterials.Where(row => row.is_active == true);
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

            foreach (PastryMaterials currentPastryMaterial in pastryMaterials)
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
        public async Task<GetPastryMaterial> GetSpecificPastryMaterial(string pastry_material_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch { return new GetPastryMaterial(); }

            List<Ingredients> ingredientsForCurrentMaterial = await _context.Ingredients.Where(x => x.is_active == true && x.pastry_material_id == currentPastryMaterial.pastry_material_id).ToListAsync();
            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients

            GetPastryMaterial response;
            try { response = await DataParser.CreatePastryMaterialResponseFromDBRow(currentPastryMaterial, _context, _kaizenTables); }
            catch (InvalidOperationException e) { return new GetPastryMaterial(); }

            await _actionLogger.LogAction(User, "GET", "Pastry Material " + currentPastryMaterial.pastry_material_id);
            return response;

        }
        [HttpGet("{pastry_material_id}/ingredients")]
        public async Task<List<GetPastryMaterialIngredients>> GetAllPastryMaterialIngredient(string pastry_material_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch { return new List<GetPastryMaterialIngredients>(); }

            List<Ingredients> currentIngredient = await _context.Ingredients.Where(x => x.pastry_material_id == pastry_material_id && x.is_active == true).ToListAsync();
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
        public async Task<IActionResult> AddNewPastryMaterial(PostPastryMaterial newEntry)
        {
            if (await DataVerification.DesignExistsAsync(newEntry.designId, _context) == false) { return NotFound(new { message = "No design with the id " + newEntry.designId + " found." }); }
            foreach (PostIngredients entry in newEntry.ingredients)
            {
                try { await DataVerification.IsIngredientItemValid(entry.itemId, entry.ingredientType, entry.amountMeasurement, _context, _kaizenTables); }
                catch (Exception e) { return BadRequest(new { message = e.Message }); }
            }
            if (newEntry.addOns != null)
            {
                foreach (PostPastryMaterialAddOns addOnEntry in newEntry.addOns)
                {
                    AddOns? selectedAddOn = null;
                    try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(addOnEntry.addOnsId, _kaizenTables); }
                    catch (Exception e) { return NotFound(new { message = e.Message }); }
                }
            }
            if (newEntry.ingredientImportance != null)
            {
                foreach (PostPastryMaterialIngredientImportance importanceEntry in newEntry.ingredientImportance)
                {
                    bool isImportanceValid = false;

                    bool inMainIngredients = newEntry.ingredients.Where(x => x.itemId == importanceEntry.itemId && x.ingredientType == importanceEntry.ingredientType).FirstOrDefault() != null ? true : false;

                    bool inSubVariantIngredients = false;
                    if (newEntry.subVariants != null) inSubVariantIngredients = newEntry.subVariants.Where(subVariant => subVariant.subVariantIngredients.Where(ingredient => ingredient.itemId == importanceEntry.itemId && ingredient.ingredientType == importanceEntry.ingredientType).IsNullOrEmpty() == false).FirstOrDefault() != null ? true : false;

                    isImportanceValid = inMainIngredients || inSubVariantIngredients;

                    if (isImportanceValid == false) return BadRequest(new { message = "One of the ingredient importance entry uses an item that does not exist in both main and sub variant ingredients, id " + importanceEntry.itemId });
                }
            }
            if (newEntry.subVariants != null)
            {
                foreach (PostPastryMaterialSubVariant entry_sub_variant in newEntry.subVariants)
                {
                    if (newEntry.mainVariantName.Equals(entry_sub_variant.subVariantName)) { return BadRequest(new { message = "Sub variants cannot have the same name as the main variant" }); }
                    foreach (PostPastryMaterialSubVariantIngredients entry_sub_variant_ingredients in entry_sub_variant.subVariantIngredients)
                    {
                        try { await DataVerification.IsIngredientItemValid(entry_sub_variant_ingredients.itemId, entry_sub_variant_ingredients.ingredientType, entry_sub_variant_ingredients.amountMeasurement, _context, _kaizenTables); }
                        catch (Exception e) { return BadRequest(new { message = e.Message }); }
                    }
                    if (entry_sub_variant.subVariantAddOns != null)
                    {
                        foreach (PostPastryMaterialSubVariantAddOns entry_sub_variant_add_on in entry_sub_variant.subVariantAddOns)
                        {
                            AddOns? selectedAddOn = null;
                            try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(entry_sub_variant_add_on.addOnsId, _kaizenTables); }
                            catch (Exception e) { return NotFound(new { message = e.Message }); }
                        }
                    }
                }
            }

            PastryMaterials newPastryMaterialEntry = new PastryMaterials();

            DateTime currentTime = DateTime.Now;
            string newPastryMaterialId = await IdFormat.GetNewestPastryMaterialId(_context);

            newPastryMaterialEntry.pastry_material_id = newPastryMaterialId;
            newPastryMaterialEntry.main_variant_name = newEntry.mainVariantName;
            newPastryMaterialEntry.design_id = newEntry.designId;
            newPastryMaterialEntry.date_added = currentTime;
            newPastryMaterialEntry.last_modified_date = currentTime;
            newPastryMaterialEntry.is_active = true;

            await _context.PastryMaterials.AddAsync(newPastryMaterialEntry);
            await _context.SaveChangesAsync();

            await DataInsertion.AddPastryMaterialIngredient(newPastryMaterialId, newEntry.ingredients, _context);
            if (newEntry.addOns != null) await DataInsertion.AddPastryMaterialAddOns(newPastryMaterialId, newEntry.addOns, _context);
            if (newEntry.ingredientImportance != null) await DataInsertion.AddPastryMaterialIngredientImportance(newPastryMaterialId, newEntry.ingredientImportance, _context);
            await _context.SaveChangesAsync();

            if (newEntry.subVariants != null)
            {
                foreach (PostPastryMaterialSubVariant entry_sub_variant in newEntry.subVariants)
                {
                    string lastPastryMaterialSubVariantId = await IdFormat.GetNewestPastryMaterialSubVariantId(_context);

                    PastryMaterialSubVariants newSubMaterialDbEntry = new PastryMaterialSubVariants();
                    newSubMaterialDbEntry.pastry_material_sub_variant_id = lastPastryMaterialSubVariantId;
                    newSubMaterialDbEntry.pastry_material_id = newPastryMaterialId;
                    newSubMaterialDbEntry.sub_variant_name = entry_sub_variant.subVariantName;
                    newSubMaterialDbEntry.date_added = currentTime;
                    newSubMaterialDbEntry.last_modified_date = currentTime;
                    newSubMaterialDbEntry.is_active = true;

                    await _context.PastryMaterialSubVariants.AddAsync(newSubMaterialDbEntry);
                    await _context.SaveChangesAsync();

                    await DataInsertion.AddPastryMaterialSubVariantIngredient(lastPastryMaterialSubVariantId, entry_sub_variant.subVariantIngredients, _context);
                    if (entry_sub_variant.subVariantAddOns != null) await DataInsertion.AddPastryMaterialSubVariantAddOn(lastPastryMaterialSubVariantId, entry_sub_variant.subVariantAddOns, _context);

                }
            }
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add Pastry Materials " + newPastryMaterialEntry.pastry_material_id);
            return Ok(new { message = "Data inserted to the database." });

        }
        [HttpPost("{pastry_material_id}/ingredients")]
        public async Task<IActionResult> AddNewPastryMaterialIngredient(string pastry_material_id, PostIngredients entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            try { await DataVerification.IsIngredientItemValid(entry.itemId, entry.ingredientType, entry.amountMeasurement, _context, _kaizenTables); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }

            string newIngredientId = await DataInsertion.AddPastryMaterialIngredient(currentPastryMaterial.pastry_material_id, entry, _context);
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add Ingredient " + newIngredientId + " to " + pastry_material_id);
            return Ok(new { message = "Data inserted to the database." });

        }
        [HttpPost("{pastry_material_id}/ingredient-importance")]
        public async Task<IActionResult> AddNewPastryMaterialIngredientImportance(string pastry_material_id, PostPastryMaterialIngredientImportance entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            if (await DataVerification.DoesIngredientExistsInPastryMaterial(pastry_material_id, entry.itemId, entry.ingredientType, _context) == false) { return BadRequest(new { message = "Item with the id " + entry.itemId + " does not exist in both the main variant and sub variants of the pastry material " + pastry_material_id }); }

            PastryMaterialIngredientImportance? importanceForSelectedIngredient = await _context.PastryMaterialIngredientImportance.Where(x => x.is_active == true && x.pastry_material_id == pastry_material_id && x.item_id == entry.itemId).FirstOrDefaultAsync();

            if (importanceForSelectedIngredient != null) return BadRequest(new { message = "Importance entry for the item with the id " + entry.itemId + " and ingredient type " + entry.ingredientType + " already exists, please modify that instead" });

            Guid newImportanceEntryId = await DataInsertion.AddPastryMaterialIngredientImportance(pastry_material_id, entry, _context);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Data inserted to the database." });
        }
        [HttpPost("{pastry_material_id}/add_ons")]
        public async Task<IActionResult> AddNewPastryMaterialAddOn(string pastry_material_id, PostPastryMaterialAddOns entry)
        {
            PastryMaterials? currentPastryMaterial = null;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            AddOns? selectedAddOn = null;
            try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(entry.addOnsId, _kaizenTables); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            string newAddOnId = await DataInsertion.AddPastryMaterialAddOns(pastry_material_id, entry, _context);
            await _context.SaveChangesAsync();

            return Ok(new { message = "New add on added to " + pastry_material_id });

        }
        [HttpPost("{pastry_material_id}/sub-variants")]
        public async Task<IActionResult> AddNewPastryMaterialSubVariant(string pastry_material_id, PostPastryMaterialSubVariant entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }
            if (currentPastryMaterial.main_variant_name.Equals(entry.subVariantName)) { return BadRequest(new { message = "Sub variants cannot have the same name as the main variant" }); }

            //Ingredient Verification
            foreach (PostPastryMaterialSubVariantIngredients subVariantIngredient in entry.subVariantIngredients)
            {
                try { await DataVerification.IsIngredientItemValid(subVariantIngredient.itemId, subVariantIngredient.ingredientType, subVariantIngredient.amountMeasurement, _context, _kaizenTables); }
                catch (Exception e) { return BadRequest(new { message = e.Message }); }
            }
            //Add on verification
            if (entry.subVariantAddOns != null)
            {
                foreach (PostPastryMaterialSubVariantAddOns subVariantAddOn in entry.subVariantAddOns)
                {
                    AddOns? selectedAddOn = null;
                    try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(subVariantAddOn.addOnsId, _kaizenTables); }
                    catch (Exception e) { return NotFound(new { message = e.Message }); }
                }
            }

            DateTime currentTime = DateTime.Now;
            string lastPastryMaterialSubVariantId = await IdFormat.GetNewestPastryMaterialSubVariantId(_context);

            PastryMaterialSubVariants newSubMaterialDbEntry = new PastryMaterialSubVariants();
            newSubMaterialDbEntry.pastry_material_sub_variant_id = lastPastryMaterialSubVariantId;
            newSubMaterialDbEntry.pastry_material_id = pastry_material_id;
            newSubMaterialDbEntry.sub_variant_name = entry.subVariantName;
            newSubMaterialDbEntry.date_added = currentTime;
            newSubMaterialDbEntry.last_modified_date = currentTime;
            newSubMaterialDbEntry.is_active = true;

            await _context.PastryMaterialSubVariants.AddAsync(newSubMaterialDbEntry);
            await _context.SaveChangesAsync();

            await DataInsertion.AddPastryMaterialSubVariantIngredient(lastPastryMaterialSubVariantId, entry.subVariantIngredients, _context);
            if (entry.subVariantAddOns != null) { await DataInsertion.AddPastryMaterialSubVariantAddOn(lastPastryMaterialSubVariantId, entry.subVariantAddOns, _context); }

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add Sub variant " + lastPastryMaterialSubVariantId + " for " + pastry_material_id);
            return Ok(new { message = "New sub variant for " + pastry_material_id + " added" });
        }
        [HttpPost("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/ingredients")]
        public async Task<IActionResult> AddNewPastryMaterialSubVariantIngredient(string pastry_material_id, string pastry_material_sub_variant_id, PostPastryMaterialSubVariantIngredients entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(currentPastryMaterial.pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            try { await DataVerification.IsIngredientItemValid(entry.itemId, entry.ingredientType, entry.amountMeasurement, _context, _kaizenTables); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }

            await DataInsertion.AddPastryMaterialSubVariantIngredient(currentPastryMaterialSubVariant.pastry_material_sub_variant_id, entry, _context);
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add Sub variant ingredient for " + pastry_material_sub_variant_id + " of " + pastry_material_id);
            return Ok(new { message = "New sub variant ingredient for " + pastry_material_sub_variant_id + " added" });
        }
        [HttpPost("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/add_ons")]
        public async Task<IActionResult> AddNewPastryMaterialSubVariantAddOn(string pastry_material_id, string pastry_material_sub_variant_id, PostPastryMaterialSubVariantAddOns entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(currentPastryMaterial.pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            AddOns? selectedAddOn = null;
            try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(entry.addOnsId, _kaizenTables); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            await DataInsertion.AddPastryMaterialSubVariantAddOn(currentPastryMaterialSubVariant.pastry_material_sub_variant_id, entry, _context);
            await _context.SaveChangesAsync();

            return Ok(new { message = "New add on inserted to " + pastry_material_sub_variant_id });
        }

        //PATCH
        [HttpPatch("{pastry_material_id}")]
        public async Task<IActionResult> UpdatePastryMaterial(string pastry_material_id, PatchPastryMaterials entry)
        {
            if (await DataVerification.DesignExistsAsync(entry.designId, _context) == false) { return NotFound(new { message = "No design with the id " + entry.designId + " found." }); }

            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            DateTime currentTime = DateTime.Now;

            _context.PastryMaterials.Update(currentPastryMaterial);
            currentPastryMaterial.design_id = entry.designId;
            currentPastryMaterial.main_variant_name = entry.mainVariantName;
            currentPastryMaterial.last_modified_date = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update Pastry Material " + pastry_material_id);
            return Ok(new { message = "Pastry Material updated." });

        }
        [HttpPatch("{pastry_material_id}/ingredients/{ingredient_id}")]
        public async Task<IActionResult> UpdatePastryMaterialIngredient(string pastry_material_id, string ingredient_id, PatchIngredients entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            Ingredients? currentIngredient;
            try { currentIngredient = await DataRetrieval.GetPastryMaterialIngredientAsync(pastry_material_id, ingredient_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            try { await DataVerification.IsIngredientItemValid(entry.itemId, entry.ingredientType, entry.amountMeasurement, _context, _kaizenTables); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }

            DateTime currentTime = DateTime.Now;

            _context.Ingredients.Update(currentIngredient);
            currentIngredient.item_id = entry.itemId;
            currentIngredient.amount = entry.amount;
            currentIngredient.amount_measurement = entry.amountMeasurement;
            currentIngredient.last_modified_date = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update Pastry Material Ingredient " + pastry_material_id);
            return Ok(new { message = "Pastry Material Ingredient updated." });

        }
        [HttpPatch("{pastry_material_id}/ingredient-importance/{pastry_material_ingredient_importance_id}")]
        public async Task<IActionResult> UpdatePastryMaterialIngredientImportance(string pastry_material_id, Guid pastry_material_ingredient_importance_id, PatchPastryMaterialIngredientImportance entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialIngredientImportance? currentIngredientImportance;
            try { currentIngredientImportance = await DataRetrieval.GetPastryMaterialIngredientImportanceAsync(pastry_material_id, pastry_material_ingredient_importance_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            if (await DataVerification.DoesIngredientExistsInPastryMaterial(pastry_material_id, entry.itemId, entry.ingredientType, _context) == false) { return BadRequest(new { message = "Item with the id " + entry.itemId + " does not exist in both the main variant and sub variants of the pastry material " + pastry_material_id }); }

            PastryMaterialIngredientImportance? importanceForSelectedIngredient = await _context.PastryMaterialIngredientImportance.Where(x => x.is_active == true && x.pastry_material_id == pastry_material_id && x.item_id == entry.itemId && x.pastry_material_ingredient_importance_id.Equals(currentIngredientImportance.pastry_material_ingredient_importance_id) == false).FirstOrDefaultAsync();

            if (importanceForSelectedIngredient != null) return BadRequest(new { message = "Importance entry for the item with the id " + entry.itemId + " and ingredient type " + entry.ingredientType + " already exists, please modify that instead" });

            DateTime currentTime = DateTime.Now;

            _context.PastryMaterialIngredientImportance.Update(currentIngredientImportance);
            currentIngredientImportance.item_id = entry.itemId;
            currentIngredientImportance.importance = entry.importance;
            currentIngredientImportance.ingredient_type = entry.ingredientType;
            currentIngredientImportance.last_modified_date = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update Pastry Material Ingredient " + pastry_material_id);
            return Ok(new { message = "Pastry Material Ingredient updated." });

        }
        [HttpPatch("{pastry_material_id}/add_ons/{pastry_material_add_on_id}")]
        public async Task<IActionResult> UpdatePastryMaterialAddOn(string pastry_material_id, string pastry_material_add_on_id, PatchPastryMaterialAddOn entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialAddOns? currentPastryMaterialAddOn = null;
            try { currentPastryMaterialAddOn = await DataRetrieval.GetPastryMaterialAddOnAsync(pastry_material_id, pastry_material_add_on_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            AddOns? selectedAddOn = null;
            try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(entry.addOnsId, _kaizenTables); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            _context.PastryMaterialAddOns.Update(currentPastryMaterialAddOn);
            currentPastryMaterialAddOn.add_ons_id = selectedAddOn.add_ons_id;
            currentPastryMaterialAddOn.amount = entry.amount;
            currentPastryMaterialAddOn.last_modified_date = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Add on " + pastry_material_add_on_id + " updated " });
        }
        [HttpPatch("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}")]
        public async Task<IActionResult> UpdatePastryMaterialSubVariant(string pastry_material_id, string pastry_material_sub_variant_id, PatchPastryMaterialSubVariants entry)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }
            if (currentPastryMaterial.main_variant_name.Equals(entry.subVariantName)) { return BadRequest(new { message = "Sub variants cannot have the same name as the main variant" }); }

            _context.PastryMaterialSubVariants.Update(currentPastryMaterialSubVariant);
            currentPastryMaterialSubVariant.last_modified_date = DateTime.Now;
            currentPastryMaterialSubVariant.sub_variant_name = entry.subVariantName;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update sub variant " + pastry_material_sub_variant_id);
            return Ok(new { message = "Sub variant updated" });
        }
        [HttpPatch("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/ingredients/{pastry_material_sub_variant_ingredient_id}")]
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

            try { await DataVerification.IsIngredientItemValid(entry.itemId, entry.ingredientType, entry.amountMeasurement, _context, _kaizenTables); }
            catch (Exception e) { return BadRequest(new { message = e.Message }); }

            _context.PastryMaterialSubVariantIngredients.Update(currentPastryMaterialSubVariantIngredient);
            currentPastryMaterialSubVariantIngredient.item_id = entry.itemId;
            currentPastryMaterialSubVariantIngredient.ingredient_type = entry.ingredientType;
            currentPastryMaterialSubVariantIngredient.amount = entry.amount;
            currentPastryMaterialSubVariantIngredient.amount_measurement = entry.amountMeasurement;
            currentPastryMaterialSubVariantIngredient.last_modified_date = DateTime.Now;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update sub variant " + pastry_material_sub_variant_id + " ingredient " + pastry_material_sub_variant_ingredient_id);
            return Ok(new { message = "Sub variant updated" });
        }
        [HttpPatch("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/add_ons/{pastry_material_sub_variant_add_on_id}")]
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
            try { selectedAddOn = await DataRetrieval.GetAddOnItemAsync(entry.addOnsId, _kaizenTables); }
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
        public async Task<IActionResult> DeletePastryMaterial(string pastry_material_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            if (currentPastryMaterial == null) { return NotFound(new { message = "No Pastry Material with the specified id found." }); }

            DateTime currentTime = DateTime.Now;

            List<Ingredients> ingredients = await _context.Ingredients.Where(x => x.pastry_material_id == pastry_material_id && x.is_active == true).ToListAsync();

            foreach (Ingredients i in ingredients)
            {
                _context.Ingredients.Update(i);
                i.last_modified_date = currentTime;
                i.is_active = false;
            }
            _context.PastryMaterials.Update(currentPastryMaterial);
            currentPastryMaterial.is_active = false;
            currentPastryMaterial.last_modified_date = currentTime;

            await _context.SaveChangesAsync();
            await _actionLogger.LogAction(User, "DELETE", "Delete Pastry Material " + pastry_material_id);
            return Ok(new { message = "Pastry Material deleted." });
        }
        [HttpDelete("{pastry_material_id}/ingredients/{ingredient_id}")]
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
            ingredientAboutToBeDeleted.is_active = false;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Delete Pastry Material Ingredient " + ingredient_id);
            return Ok(new { message = "Ingredient deleted." });
        }
        [HttpDelete("{pastry_material_id}/ingredient-importance/{pastry_material_ingredient_importance_id}")]
        public async Task<IActionResult> DeletePastryMaterialIngredientImportance(string pastry_material_id, Guid pastry_material_ingredient_importance_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialIngredientImportance? currentIngredientImportance;
            try { currentIngredientImportance = await DataRetrieval.GetPastryMaterialIngredientImportanceAsync(pastry_material_id, pastry_material_ingredient_importance_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            DateTime currentTime = DateTime.Now;

            _context.PastryMaterialIngredientImportance.Update(currentIngredientImportance);
            currentIngredientImportance.last_modified_date = currentTime;
            currentIngredientImportance.is_active = false;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update Pastry Material Ingredient " + pastry_material_id);
            return Ok(new { message = "Pastry Material Ingredient updated." });

        }
        [HttpDelete("{pastry_material_id}/add_ons/{pastry_material_add_on_id}")]
        public async Task<IActionResult> DeletePastryMaterialAddOn(string pastry_material_id, string pastry_material_add_on_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialAddOns? currentPastryMaterialAddOn = null;
            try { currentPastryMaterialAddOn = await DataRetrieval.GetPastryMaterialAddOnAsync(pastry_material_id, pastry_material_add_on_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            _context.PastryMaterialAddOns.Update(currentPastryMaterialAddOn);
            currentPastryMaterialAddOn.is_active = false;
            currentPastryMaterialAddOn.last_modified_date = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Add on " + pastry_material_add_on_id + " deleted " });
        }
        [HttpDelete("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}")]
        public async Task<IActionResult> DeletePastryMaterialVariant(string pastry_material_id, string pastry_material_sub_variant_id)
        {
            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            PastryMaterialSubVariants? currentPastryMaterialSubVariant = null;
            try { currentPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(pastry_material_id, pastry_material_sub_variant_id, _context); }
            catch (Exception e) { return NotFound(new { message = e.Message }); }

            _context.PastryMaterialSubVariants.Update(currentPastryMaterialSubVariant);
            currentPastryMaterialSubVariant.is_active = false;
            currentPastryMaterialSubVariant.last_modified_date = DateTime.Now;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Delete sub variant " + pastry_material_sub_variant_id);
            return Ok(new { message = "Sub variant deleted" });
        }
        [HttpDelete("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/ingredients/{pastry_material_sub_variant_ingredient_id}")]
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
            currentPastryMaterialSubVariantIngredient.is_active = false;
            currentPastryMaterialSubVariantIngredient.last_modified_date = DateTime.Now;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Delete sub variant " + pastry_material_sub_variant_id + " ingredient " + pastry_material_sub_variant_ingredient_id);
            return Ok(new { message = "Sub variant deleted" });
        }
        [HttpDelete("{pastry_material_id}/sub-variants/{pastry_material_sub_variant_id}/add_ons/{pastry_material_sub_variant_add_on_id}")]
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
            currentPastryMaterialSubVariantAddOn.is_active = false;
            currentPastryMaterialSubVariantAddOn.last_modified_date = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Add on " + pastry_material_sub_variant_add_on_id + " deleted " });
        }

    }
}
