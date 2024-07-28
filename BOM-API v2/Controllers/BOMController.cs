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

namespace API_TEST.Controllers
{
    //
    // TESTING END
    //

    [ApiController]
    [Route("debug")]
    public class TestEndpointsController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        private readonly IActionLogger _actionLogger;

        public TestEndpointsController(DatabaseContext context, IActionLogger logger, KaizenTables kaizenTables)
        {
            _context = context;
            _actionLogger = logger;
            _kaizenTables = kaizenTables;
        }

        [HttpGet("sss/{variant_id}")]
        public async Task<double> TestEndp(string variant_id)
        {
            return await PriceCalculator.CalculatePastryMaterialPrice(variant_id, _context, _kaizenTables);
        }
    }

    [ApiController]
    [Route("data-analysis")]
    [Authorize(Roles = UserRoles.Admin)]
    public class BOMController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly IActionLogger _actionLogger;
        private readonly KaizenTables _kaizenTables;

        public BOMController(DatabaseContext context, IActionLogger logger, KaizenTables kaizenTables)
        {
            _context = context;
            _actionLogger = logger;
            _kaizenTables = kaizenTables;
        }

        //!!!MIGHT HAVE BUGS!!!
        [HttpGet("item-used/occurrence/")]
        public async Task<List<GetUsedItemsByOccurence>> GetMostCommonlyUsedItems(string? sortBy, string? sortOrder)
        {
            List<Ingredients> ingredientsItems = _context.Ingredients.Where(row => row.isActive == true).Select(row => new Ingredients() { item_id = row.item_id, ingredient_type = row.ingredient_type, PastryMaterials = row.PastryMaterials }).ToList();
            List<MaterialIngredients> materialIngredientsItems = _context.MaterialIngredients.Where(row => row.isActive == true).Select(row => new MaterialIngredients() { item_id = row.item_id, ingredient_type = row.ingredient_type, Materials = row.Materials }).ToList();

            //Lists for checking if records referenced by ingredients and material_ingredients are active are active
            List<Materials> activeMaterials = await _context.Materials.Where(x => x.isActive == true).Select(x => new Materials() { material_id = x.material_id, material_name = x.material_name }).ToListAsync();
            List<Item> activeInventoryItems = await _kaizenTables.Item.Where(x => x.isActive == true).ToListAsync();  //Replace with function to get all active inventory items 
            List<Designs> allDesigns = await _context.Designs.Where(x => x.isActive == true).ToListAsync();

            if (ingredientsItems.IsNullOrEmpty() && materialIngredientsItems.IsNullOrEmpty()) { return new List<GetUsedItemsByOccurence>(); }
            if (activeInventoryItems.IsNullOrEmpty()) { return new List<GetUsedItemsByOccurence>(); }


            List<GetUsedItemsByOccurence> response = new List<GetUsedItemsByOccurence>();

            //Dictionaries for ratio calculation
            //Dictionary<string, double> occurenceAsPastryIngredient = new Dictionary<string, double>();
            //Dictionary<string, double> occurenceAsMaterialIngredient = new Dictionary<string, double>();

            double totalNumberOfPastryIngredients = 0;
            double totalNumberOfMaterialIngredients = 0;

            //Count the ingredients
            using (var ingItemEnum = ingredientsItems.GetEnumerator())
            {
                while (ingItemEnum.MoveNext())
                {
                    Designs? currentParentPastryMaterialReferencedDesign = null;
                    try { currentParentPastryMaterialReferencedDesign = allDesigns.Where(x => x.design_id.SequenceEqual(ingItemEnum.Current.PastryMaterials.design_id)).First(); }
                    catch (Exception e) { continue; }

                    string currentItemName = "N/A";
                    switch (ingItemEnum.Current.ingredient_type)
                    {
                        //Insert code to check if inventory item is active here
                        case IngredientType.InventoryItem:
                            //!!!UNTESTED!!!
                            Item? searchResultI = null;
                            try { searchResultI = activeInventoryItems.Find(x => x.id == Convert.ToInt32(ingItemEnum.Current.item_id)); }
                            catch { continue; }
                            if (searchResultI == null) { continue; }

                            if (searchResultI == null) continue;
                            currentItemName = searchResultI.item_name;
                            break;
                        case IngredientType.Material:
                            Materials? searchResultM = activeMaterials.Find(x => x.material_id == ingItemEnum.Current.item_id);
                            if (searchResultM == null) continue;

                            currentItemName = searchResultM.material_name; break;
                    }

                    GetUsedItemsByOccurence? currentRecord = response.Find(x => x.item_id == ingItemEnum.Current.item_id);

                    if (currentRecord == null)
                    {
                        GetUsedItemsByOccurence newEntry = new GetUsedItemsByOccurence()
                        {
                            item_id = ingItemEnum.Current.item_id,
                            item_name = currentItemName,
                            item_type = ingItemEnum.Current.ingredient_type,
                            as_material_ingredient = new List<string>(),
                            as_cake_ingredient = new List<string>(),
                            num_of_uses_cake_ingredient = 0,
                            num_of_uses_material_ingredient = 0,
                            ratio_of_uses_cake_ingredient = 0.0,
                            ratio_of_uses_material_ingredient = 0.0
                        };

                        response.Add(newEntry);
                        currentRecord = response.Find(x => x.item_id == ingItemEnum.Current.item_id);
                    }
                    totalNumberOfPastryIngredients += 1;
                    currentRecord.num_of_uses_cake_ingredient += 1;
                    currentRecord.as_cake_ingredient.Add(ingItemEnum.Current.PastryMaterials.pastry_material_id + ": " + " " + currentParentPastryMaterialReferencedDesign.display_name);
                }
            }
            //Count the material ingredients
            using (var matIngItemEnum = materialIngredientsItems.GetEnumerator())
            {
                while (matIngItemEnum.MoveNext())
                {
                    string currentItemName = "N/A";
                    switch (matIngItemEnum.Current.ingredient_type)
                    {
                        //Insert code to check if inventory item is activw here
                        case IngredientType.InventoryItem:
                            //!!!UNTESTED!!!
                            Item? searchResultI = null;
                            try { searchResultI = activeInventoryItems.Find(x => x.id == Convert.ToInt32(matIngItemEnum.Current.item_id)); }
                            catch { continue; }
                            if (searchResultI == null) { continue; }

                            if (searchResultI == null) continue;
                            currentItemName = searchResultI.item_name;
                            break;
                        case IngredientType.Material:

                            Materials? searchResult = activeMaterials.Find(x => x.material_id == matIngItemEnum.Current.item_id);
                            if (searchResult == null) continue;

                            currentItemName = searchResult.material_name; break;
                    }

                    GetUsedItemsByOccurence? currentRecord = response.Find(x => x.item_id == matIngItemEnum.Current.item_id);
                    if (currentRecord == null)
                    {
                        GetUsedItemsByOccurence newEntry = new GetUsedItemsByOccurence()
                        {
                            item_id = matIngItemEnum.Current.item_id,
                            item_name = currentItemName,
                            item_type = matIngItemEnum.Current.ingredient_type,
                            as_material_ingredient = new List<string>(),
                            as_cake_ingredient = new List<string>(),
                            num_of_uses_cake_ingredient = 0,
                            num_of_uses_material_ingredient = 0,
                            ratio_of_uses_cake_ingredient = 0.0,
                            ratio_of_uses_material_ingredient = 0.0
                        };

                        response.Add(newEntry);
                        currentRecord = response.Find(x => x.item_id == matIngItemEnum.Current.item_id);
                    }
                    totalNumberOfMaterialIngredients += 1;
                    currentRecord.num_of_uses_material_ingredient += 1;
                    currentRecord.as_material_ingredient.Add(matIngItemEnum.Current.Materials.material_id + ": " + matIngItemEnum.Current.Materials.material_name);
                }
            }
            //Ratio calculation
            foreach (GetUsedItemsByOccurence currentResponseRow in response)
            {
                double curRowNumOfUsesCakeIng = currentResponseRow.num_of_uses_cake_ingredient;
                double curRowNumOfUsesMatIng = currentResponseRow.num_of_uses_material_ingredient;

                currentResponseRow.ratio_of_uses_cake_ingredient = curRowNumOfUsesCakeIng <= 0 ? 0 : curRowNumOfUsesCakeIng / totalNumberOfPastryIngredients;
                currentResponseRow.ratio_of_uses_material_ingredient = curRowNumOfUsesMatIng <= 0 ? 0 : curRowNumOfUsesMatIng / totalNumberOfMaterialIngredients;
            }
            //Sorting Algorithm
            if (sortBy != null)
            {

                sortOrder = sortOrder == null ? "ASC" : sortOrder.ToUpper() != "ASC" && sortOrder.ToUpper() != "DESC" ? "ASC" : sortOrder.ToUpper();

                switch (sortBy)
                {
                    case "item_id":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.item_id.CompareTo(x.item_id));
                                break;
                            default:
                                response.Sort((x, y) => x.item_id.CompareTo(y.item_id));
                                break;
                        }
                        break;
                    case "item_name":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.item_name.CompareTo(x.item_name));
                                break;
                            default:
                                response.Sort((x, y) => x.item_name.CompareTo(y.item_name));
                                break;
                        }
                        break;
                    case "item_type":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.item_type.CompareTo(x.item_type));
                                break;
                            default:
                                response.Sort((x, y) => x.item_type.CompareTo(y.item_type));
                                break;
                        }
                        break;
                    case "num_of_uses_cake_ingredient":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.num_of_uses_cake_ingredient.CompareTo(x.num_of_uses_cake_ingredient));
                                break;
                            default:
                                response.Sort((x, y) => x.num_of_uses_cake_ingredient.CompareTo(y.num_of_uses_cake_ingredient));
                                break;
                        }
                        break;
                    case "num_of_uses_material_ingredient":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.num_of_uses_material_ingredient.CompareTo(x.num_of_uses_material_ingredient));
                                break;
                            default:
                                response.Sort((x, y) => x.num_of_uses_material_ingredient.CompareTo(y.num_of_uses_material_ingredient));
                                break;
                        }
                        break;
                    case "ratio_of_uses_material_ingredient":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.ratio_of_uses_material_ingredient.CompareTo(x.ratio_of_uses_material_ingredient));
                                break;
                            default:
                                response.Sort((x, y) => x.ratio_of_uses_material_ingredient.CompareTo(y.ratio_of_uses_material_ingredient));
                                break;
                        }
                        break;
                    case "ratio_of_uses_cake_ingredient":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.ratio_of_uses_cake_ingredient.CompareTo(x.ratio_of_uses_cake_ingredient));
                                break;
                            default:
                                response.Sort((x, y) => x.ratio_of_uses_cake_ingredient.CompareTo(y.ratio_of_uses_cake_ingredient));
                                break;
                        }
                        break;
                }
            }

            await _actionLogger.LogAction(User, "GET", "Most Commonly Used Items ");
            return response;
        }

        //!!!UNTESTED!!!
        [HttpGet("item-used/seasonal-occurrence")]
        public async Task<List<GetUsedItemsBySeasonalTrends>> GetIngredientTrendsByMonths(string? sortOrder)
        {
            List<Orders> ordersList = await _kaizenTables.Orders.Where(x => x.is_active == true).ToListAsync();
            if (ordersList.IsNullOrEmpty()) { return new List<GetUsedItemsBySeasonalTrends>(); }

            List<PastryMaterials> allPastryMaterials = await _context.PastryMaterials.Where(x => x.isActive == true).ToListAsync();
            if (allPastryMaterials.IsNullOrEmpty()) { return new List<GetUsedItemsBySeasonalTrends>(); }

            List<Item> allInventoryItems = await _kaizenTables.Item.Where(x => x.isActive == true).ToListAsync();
            if (allInventoryItems.IsNullOrEmpty()) { return new List<GetUsedItemsBySeasonalTrends>(); }

            List<Ingredients> ingredientsItems = await _context.Ingredients.Where(row => row.isActive == true).Select(row => new Ingredients() { item_id = row.item_id, ingredient_type = row.ingredient_type, PastryMaterials = row.PastryMaterials }).ToListAsync();
            List<MaterialIngredients> materialIngredientsItems = await _context.MaterialIngredients.Where(x => x.Materials.isActive == true && x.isActive == true).ToListAsync();
            if (materialIngredientsItems.IsNullOrEmpty() && ingredientsItems.IsNullOrEmpty()) { return new List<GetUsedItemsBySeasonalTrends>(); }


            List<GetUsedItemsBySeasonalTrends> response = new List<GetUsedItemsBySeasonalTrends>(); //List to return

            Orders? oldestRecord = ordersList.MaxBy(x => x.created_at);
            Orders? newestRecord = ordersList.MinBy(x => x.created_at);

            Dictionary<string, int> occurenceCount = new Dictionary<string, int>(); //Stores the count of occurence for ingredients in the current loop below

            foreach (DateTime currentDate in Iterators.LoopThroughMonths(oldestRecord.created_at, newestRecord.created_at))
            {
                GetUsedItemsBySeasonalTrends newResponseEntry = new GetUsedItemsBySeasonalTrends();
                int currentDateYear = currentDate.Year;
                int currentDateMonth = currentDate.Month;

                int totalNumberOfIngredientsInInterval = 0;

                newResponseEntry.date_start = new DateTime(currentDateYear, currentDateMonth, 1);
                newResponseEntry.date_end = new DateTime(currentDateYear, currentDateMonth, DateTime.DaysInMonth(currentDateYear, currentDateMonth));

                newResponseEntry.item_list = new List<ItemOccurence>();

                List<Orders> ordersForCurrentDate = ordersList.Where(x => x.created_at >= newResponseEntry.date_start && x.created_at <= newResponseEntry.date_end).ToList();

                //Add all items in the list of occurence for the current month
                foreach (Orders currentOrder in ordersForCurrentDate)
                {
                    DateTime currentOrderCreationDate = currentOrder.created_at;
                    byte[] currentOrderDesignId = currentOrder.design_id;

                    PastryMaterials? currentOrderDesignRow = allPastryMaterials.Find(x => x.design_id == currentOrderDesignId);
                    if (currentOrderDesignRow != null) { continue; }

                    List<Ingredients> currentOrderCakeIngredients = ingredientsItems.Where(x => x.pastry_material_id == currentOrderDesignRow.pastry_material_id).ToList();
                    if (currentOrderCakeIngredients.IsNullOrEmpty()) { continue; }

                    foreach (Ingredients i in currentOrderCakeIngredients)
                    {
                        switch (i.ingredient_type)
                        {
                            case IngredientType.InventoryItem:
                                {
                                    //Add check here if the inventory item exists
                                    //
                                    //

                                    //!!!UNTESTED!!!
                                    Item? currentInventoryItem = null;
                                    try { currentInventoryItem = allInventoryItems.Find(x => x.id == Convert.ToInt32(i.item_id)); }
                                    catch { continue; }
                                    if (currentInventoryItem == null) { continue; }

                                    ItemOccurence? currentOccurenceEntry = newResponseEntry.item_list.Find(x => x.item_id == i.item_id);
                                    if (currentOccurenceEntry == null)
                                    {
                                        newResponseEntry.item_list.Add(new ItemOccurence()
                                        {
                                            item_id = i.item_id,
                                            item_name = currentInventoryItem.item_name, //Add code here to find the item name in inventory
                                            item_type = i.ingredient_type,
                                            occurrence_count = 1
                                        });
                                    }
                                    else { currentOccurenceEntry.occurrence_count += 1; }

                                    totalNumberOfIngredientsInInterval += 1;
                                    break;
                                }
                            case IngredientType.Material:
                                {
                                    List<MaterialIngredients> currentMaterialIngredients = materialIngredientsItems.Where(x => x.material_id == i.item_id).ToList();

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
                                            List<MaterialIngredients> newEntriesToLoopThru = materialIngredientsItems.FindAll(x => x.material_id == currentMatIngInLoop.item_id);
                                            currentMaterialIngredients.AddRange(newEntriesToLoopThru);
                                        }
                                        currentIndex += 1;
                                    }

                                    //Removes all the entries for the material
                                    //As the sub-ingredients for them is already in the list
                                    currentMaterialIngredients.RemoveAll(x => x.ingredient_type == IngredientType.Material);

                                    foreach (MaterialIngredients currentMaterialIngredient in currentMaterialIngredients)
                                    {
                                        //!!!UNTESTED!!!
                                        Item? currentInventoryItem = null;
                                        try { currentInventoryItem = allInventoryItems.Find(x => x.id == Convert.ToInt32(currentMaterialIngredient.item_id)); }
                                        catch { continue; }

                                        if (currentInventoryItem == null) continue;

                                        ItemOccurence? currentOccurenceEntry = newResponseEntry.item_list.Find(x => x.item_id == currentMaterialIngredient.item_id);

                                        if (currentOccurenceEntry == null)
                                        {
                                            newResponseEntry.item_list.Add(new ItemOccurence()
                                            {
                                                item_id = currentMaterialIngredient.item_id,
                                                item_name = currentInventoryItem.item_name, //Add code here to find the item name in inventory
                                                item_type = currentMaterialIngredient.ingredient_type,
                                                occurrence_count = 1
                                            });
                                        }
                                        else { currentOccurenceEntry.occurrence_count += 1; }
                                        totalNumberOfIngredientsInInterval += 1;
                                    }
                                    break;
                                }
                        }
                    }
                }
                //Calculate the ratio for the ingredients in the occurence list
                foreach (ItemOccurence currentItemForRatioCalculation in newResponseEntry.item_list)
                {
                    currentItemForRatioCalculation.ratio = currentItemForRatioCalculation.occurrence_count / totalNumberOfIngredientsInInterval;
                }
            }
            await _actionLogger.LogAction(User, "GET", "All items by seasonal occurence");
            return response;
        }

        [HttpGet("tags-used/occurrence/")]
        public async Task<List<GetTagOccurrence>> GetTagOccurrence(string? sortBy, string? sortOrder)
        {
            List<DesignTags> allTags = await _context.DesignTags.Where(x => x.isActive == true).ToListAsync();
            if (allTags.IsNullOrEmpty()) { return new List<GetTagOccurrence>(); }
            List<DesignTagsForCakes> allTagsForCake = await _context.DesignTagsForCakes.Where(x => x.isActive == true).ToListAsync();

            List<GetTagOccurrence> response = new List<GetTagOccurrence>();

            foreach (DesignTagsForCakes DesignTagsForCakes in allTagsForCake)
            {
                DesignTags? selectedTag = allTags.Where(x => x.design_tag_id == DesignTagsForCakes.design_tag_id).FirstOrDefault();
                if (selectedTag == null) { continue; }
                GetTagOccurrence? selectedResponseRow = response.Where(x => x.design_tag_id == selectedTag.design_tag_id).FirstOrDefault();
                if (selectedResponseRow != null) { selectedResponseRow.occurrence_count += 1; }
                else
                {
                    GetTagOccurrence newResponseEntry = new GetTagOccurrence();
                    newResponseEntry.design_tag_id = selectedTag.design_tag_id;
                    newResponseEntry.design_tag_name = selectedTag.design_tag_name;
                    newResponseEntry.occurrence_count = 1;
                    newResponseEntry.ratio = 0.0;
                    response.Add(newResponseEntry);
                }
            }
            double totalAmountOfCakeTags = Convert.ToDouble(allTagsForCake.Count());
            foreach (GetTagOccurrence currentResponseRow in response)
            {
                currentResponseRow.ratio = currentResponseRow.occurrence_count / totalAmountOfCakeTags;
            }
            //Sorting Algorithm
            if (sortBy != null)
            {

                sortOrder = sortOrder == null ? "ASC" : sortOrder.ToUpper() != "ASC" && sortOrder.ToUpper() != "DESC" ? "ASC" : sortOrder.ToUpper();

                switch (sortBy)
                {
                    case "design_tag_name":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.design_tag_name.CompareTo(x.design_tag_name));
                                break;
                            default:
                                response.Sort((x, y) => x.design_tag_name.CompareTo(y.design_tag_name));
                                break;
                        }
                        break;
                    case "occurrence_count":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.occurrence_count.CompareTo(x.occurrence_count));
                                break;
                            default:
                                response.Sort((x, y) => x.occurrence_count.CompareTo(y.occurrence_count));
                                break;
                        }
                        break;
                    case "ratio":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.ratio.CompareTo(x.ratio));
                                break;
                            default:
                                response.Sort((x, y) => x.ratio.CompareTo(y.ratio));
                                break;
                        }
                        break;
                    default:
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.design_tag_id.CompareTo(x.design_tag_id));
                                break;
                            default:
                                response.Sort((x, y) => x.design_tag_id.CompareTo(y.design_tag_id));
                                break;
                        }
                        break;
                }
            }

            await _actionLogger.LogAction(User, "GET", "Tag occurence");
            return response;
        }
    }
    [ApiController]
    public class BOMDataManipulationController : ControllerBase
    {

        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        private readonly IActionLogger _actionLogger;
        private readonly ICakePriceCalculator _cakePriceCalculator;

        public BOMDataManipulationController(DatabaseContext context, KaizenTables kaizen, IActionLogger logs, ICakePriceCalculator cakePriceCalculator) { _context = context; _actionLogger = logs; _kaizenTables = kaizen; _cakePriceCalculator = cakePriceCalculator; }

        //
        // INVENTORY ACTIONS
        //
        [HttpPost("pastry-materials/{pastry_material_id}/subtract-recipe-ingredients-on-inventory/{variant_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> SubtractPastryMaterialIngredientsOnInventory(string pastry_material_id, string variant_id)
        {
            PastryMaterials? currentPastryMaterial = await _context.PastryMaterials.FindAsync(pastry_material_id);
            PastryMaterialSubVariants? sub_variant = null;

            if (currentPastryMaterial == null) { return NotFound(new { message = "No pastry material with the specified id found" }); }
            if (currentPastryMaterial.pastry_material_id.Equals(variant_id) == false)
            {
                try { sub_variant = await _context.PastryMaterialSubVariants.Where(x => x.isActive == true && x.pastry_material_id == currentPastryMaterial.pastry_material_id && x.pastry_material_sub_variant_id.Equals(variant_id)).FirstAsync(); }
                catch { return NotFound(new { message = "No variant with the id " + variant_id + " exists" }); }
            }

            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients
            Dictionary<string, InventorySubtractorInfo>? inventoryItemsAboutToBeSubtracted = null;

            try { inventoryItemsAboutToBeSubtracted = await DataParser.GetTotalIngredientAmountList(variant_id, _context, _kaizenTables); }
            catch (FormatException e) { return BadRequest(new { message = e.Message }); }
            catch (InvalidAmountMeasurementException e) { return StatusCode(500, new { message = e.Message }); }
            catch (NotFoundInDatabaseException e) { return StatusCode(500, new { message = e.Message }); }

            List<ItemSubtractionInfo> dataForSubtractionHistory = new List<ItemSubtractionInfo>(); //For history of subtractions table
            foreach (string currentInventoryItemId in inventoryItemsAboutToBeSubtracted.Keys)
            {
                InventorySubtractorInfo currentInventorySubtractorInfo = inventoryItemsAboutToBeSubtracted[currentInventoryItemId];

                //No need to check, record already checked earlier
                Item referencedInventoryItem = await DataRetrieval.GetInventoryItemAsync(currentInventoryItemId, _kaizenTables);

                string? inventoryItemMeasurement = null;
                string? inventoryItemQuantityUnit = null;
                bool isInventoryItemMeasurementValid = false;

                //Add code here to check if the unit of the item in the inventory and the recorded total is the same
                foreach (string unitQuantity in validMeasurementUnits.Keys)
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

                double amountToBeSubtracted = 0.0;
                if (inventoryItemQuantityUnit.Equals("Count"))
                {
                    amountToBeSubtracted = currentInventorySubtractorInfo.Amount;
                    referencedInventoryItem.quantity = referencedInventoryItem.quantity - currentInventorySubtractorInfo.Amount;
                }
                else
                {
                    amountToBeSubtracted = UnitConverter.ConvertByName(currentInventorySubtractorInfo.Amount, inventoryItemQuantityUnit, currentInventorySubtractorInfo.AmountUnit, referencedInventoryItem.measurements);
                    referencedInventoryItem.quantity = referencedInventoryItem.quantity - amountToBeSubtracted;
                }


                ItemSubtractionInfo newIngredientSubtractionInfoEntry = new ItemSubtractionInfo
                {
                    item_id = Convert.ToString(referencedInventoryItem.id),
                    item_name = referencedInventoryItem.item_name,
                    amount_quantity_type = inventoryItemQuantityUnit,
                    amount_unit = referencedInventoryItem.measurements,
                    amount = amountToBeSubtracted
                };

                dataForSubtractionHistory.Add(newIngredientSubtractionInfoEntry);
                _kaizenTables.Item.Update(referencedInventoryItem);
            }

            IngredientSubtractionHistory newIngredientSubtractionHistoryEntry = new IngredientSubtractionHistory
            {
                ingredient_subtraction_history_id = new Guid(),
                item_subtraction_info = dataForSubtractionHistory,
                date_subtracted = DateTime.Now,
            };
            await _context.IngredientSubtractionHistory.AddAsync(newIngredientSubtractionHistoryEntry);

            await _kaizenTables.SaveChangesAsync();
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Subtract ingredients of " + pastry_material_id);
            return Ok(new { message = "Ingredients sucessfully deducted." });
        }
        /*
        [HttpPost("orders/{order_id}/subtract-recipe-ingredients-on-inventory/")]
        [Authorize(Roles = UserRoles.Customer)]
        public async Task<IActionResult> SubtractPastryMaterialIngredientsOnInventory(string order_id)
        {
            PastryMaterials? currentPastryMaterial = await _context.PastryMaterials.FindAsync(pastry_material_id);
            List<Ingredients> currentPastryIngredients = await _context.Ingredients.Where(x => x.isActive == true && x.pastry_material_id == pastry_material_id).ToListAsync();
            PastryMaterialSubVariants? sub_variant = null;

            if (currentPastryMaterial == null) { return NotFound(new { message = "No pastry material with the specified id found" }); }
            if (currentPastryIngredients.IsNullOrEmpty()) { return StatusCode(500, new { message = "The specified pastry material does not contain any active ingredients" }); }
            if (currentPastryMaterial.pastry_material_id.Equals(variant_id) == false)
            {
                try { sub_variant = await _context.PastryMaterialSubVariants.Where(x => x.isActive == true && x.pastry_material_id == currentPastryMaterial.pastry_material_id && x.pastry_material_sub_variant_id.Equals(variant_id)).FirstAsync(); }
                catch { return NotFound(new { message = "No variant with the id " + variant_id + " exists" }); }
            }

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

            if (currentPastryMaterial.pastry_material_id.Equals(variant_id) == false)
            {
                List<PastryMaterialSubVariantIngredients> currentVariantIngredients = await _context.PastryMaterialSubVariantIngredients.Where(x => x.isActive == true && x.pastry_material_sub_variant_id == sub_variant.pastry_material_sub_variant_id).ToListAsync();

                foreach (PastryMaterialSubVariantIngredients currentVariantIngredientsRow in currentVariantIngredients)
                {
                    //Check if the measurement unit in the ingredient record is valid
                    //If not found, skip current ingredient
                    string? amountQuantityType = null;
                    string? amountUnitMeasurement = null;

                    bool isAmountMeasurementValid = false;
                    foreach (string unitQuantity in validMeasurementUnits.Keys)
                    {
                        List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                        string? currentMeasurement = currentQuantityUnits.Find(x => x.Equals(currentVariantIngredientsRow.amount_measurement));

                        if (currentMeasurement == null) { continue; }
                        else
                        {
                            isAmountMeasurementValid = true;
                            amountQuantityType = unitQuantity;
                            amountUnitMeasurement = currentMeasurement;
                        }
                    }
                    if (isAmountMeasurementValid == false) { return BadRequest(new { message = "The measurement of the pastry material sub variant ingredient with the id " + currentVariantIngredientsRow.pastry_material_sub_variant_ingredient_id + " is not valid." }); }

                    switch (currentVariantIngredientsRow.ingredient_type)
                    {
                        case IngredientType.InventoryItem:
                            {
                                //Find the referred item
                                Item? currentRefInvItem = null;
                                try
                                { currentRefInvItem = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(currentVariantIngredientsRow.item_id)).FirstAsync(); }
                                catch (FormatException e) { return StatusCode(500, new { message = "The pastry sub variant ingredient with the type of " + IngredientType.InventoryItem + " and the ingredient id " + currentVariantIngredientsRow.pastry_material_sub_variant_ingredient_id + " cannot be parsed as an integer" }); }
                                catch (InvalidOperationException e) { return NotFound(new { message = "The pastry sub variant ingredient with the type of " + IngredientType.InventoryItem + " and the item id " + currentVariantIngredientsRow.item_id + " does not exist in the inventory" }); }

                                string currentItemMeasurement = currentVariantIngredientsRow.amount_measurement;
                                double currentItemAmount = currentVariantIngredientsRow.amount;

                                //Calculate the value to subtract here
                                InventorySubtractorInfo? inventorySubtractorInfoForCurrentIngredient = null;
                                inventoryItemsAboutToBeSubtracted.TryGetValue(currentVariantIngredientsRow.item_id, out inventorySubtractorInfoForCurrentIngredient);

                                if (inventorySubtractorInfoForCurrentIngredient == null)
                                {
                                    inventoryItemsAboutToBeSubtracted.Add(currentVariantIngredientsRow.item_id, new InventorySubtractorInfo(amountQuantityType, amountUnitMeasurement, currentVariantIngredientsRow.amount));
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
                                List<MaterialIngredients> currentMaterialIngredients = await _context.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentVariantIngredientsRow.item_id).ToListAsync();

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
                                    inventoryItemsAboutToBeSubtracted.TryGetValue(currentVariantIngredientsRow.item_id, out inventorySubtractorInfoForCurrentIngredient);
                                    if (inventorySubtractorInfoForCurrentIngredient == null)
                                    {
                                        inventoryItemsAboutToBeSubtracted.Add(currentVariantIngredientsRow.item_id, new InventorySubtractorInfo(amountQuantityType, amountUnitMeasurement, currentVariantIngredientsRow.amount));
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
            }

            List<ItemSubtractionInfo> dataForSubtractionHistory = new List<ItemSubtractionInfo>(); //For history of subtractions table
            foreach (string currentInventoryItemId in inventoryItemsAboutToBeSubtracted.Keys)
            {
                InventorySubtractorInfo currentInventorySubtractorInfo = inventoryItemsAboutToBeSubtracted[currentInventoryItemId];

                //No need to check, record already checked earlier
                Item referencedInventoryItem = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(currentInventoryItemId)).FirstAsync();

                string? inventoryItemMeasurement = null;
                string? inventoryItemQuantityUnit = null;
                bool isInventoryItemMeasurementValid = false;

                //Add code here to check if the unit of the item in the inventory and the recorded total is the same
                foreach (string unitQuantity in validMeasurementUnits.Keys)
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

                double amountToBeSubtracted = 0.0;
                if (inventoryItemQuantityUnit.Equals("Count"))
                {
                    amountToBeSubtracted = currentInventorySubtractorInfo.Amount;
                    referencedInventoryItem.quantity = referencedInventoryItem.quantity - currentInventorySubtractorInfo.Amount;
                }
                else
                {
                    amountToBeSubtracted = UnitConverter.ConvertByName(currentInventorySubtractorInfo.Amount, inventoryItemQuantityUnit, currentInventorySubtractorInfo.AmountUnit, referencedInventoryItem.measurements);
                    referencedInventoryItem.quantity = referencedInventoryItem.quantity - amountToBeSubtracted;
                }
                ItemSubtractionInfo newIngredientSubtractionInfoEntry = new ItemSubtractionInfo
                {
                    item_id = Convert.ToString(referencedInventoryItem.id),
                    item_name = referencedInventoryItem.item_name,
                    amount_quantity_type = inventoryItemQuantityUnit,
                    amount_unit = referencedInventoryItem.measurements,
                    amount = amountToBeSubtracted
                };

                dataForSubtractionHistory.Add(newIngredientSubtractionInfoEntry);
                _kaizenTables.Item.Update(referencedInventoryItem);
            }
            IngredientSubtractionHistory newIngredientSubtractionHistoryEntry = new IngredientSubtractionHistory
            {
                ingredient_subtraction_history_id = new Guid(),
                item_subtraction_info = dataForSubtractionHistory,
                date_subtracted = DateTime.Now,
            };
            await _context.IngredientSubtractionHistory.AddAsync(newIngredientSubtractionHistoryEntry);

            await _kaizenTables.SaveChangesAsync();
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Subtract ingredients of " + pastry_material_id);
            return Ok(new { message = "Ingredients sucessfully deducted." });
        }
        */
    }
}
