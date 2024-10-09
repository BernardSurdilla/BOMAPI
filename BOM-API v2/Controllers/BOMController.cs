using BillOfMaterialsAPI.Helpers;
using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BOM_API_v2.Services;
using Castle.Components.DictionaryAdapter.Xml;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UnitsNet;

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
            _kaizenTables = kaizenTables;
            _actionLogger = logger;
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
            List<Ingredients> ingredientsItems = _context.Ingredients.Where(row => row.is_active == true).Select(row => new Ingredients() { item_id = row.item_id, ingredient_type = row.ingredient_type, PastryMaterials = row.PastryMaterials }).ToList();
            List<MaterialIngredients> materialIngredientsItems = _context.MaterialIngredients.Where(row => row.is_active == true).Select(row => new MaterialIngredients() { item_id = row.item_id, ingredient_type = row.ingredient_type, Materials = row.Materials }).ToList();

            //Lists for checking if records referenced by ingredients and material_ingredients are active are active
            List<Materials> activeMaterials = await _context.Materials.Where(x => x.is_active == true).Select(x => new Materials() { material_id = x.material_id, material_name = x.material_name }).ToListAsync();
            List<Item> activeInventoryItems = await _kaizenTables.Item.Where(x => x.is_active == true).ToListAsync();  //Replace with function to get all active inventory items 
            List<Designs> allDesigns = await _context.Designs.Where(x => x.is_active == true).ToListAsync();

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
                    try { currentParentPastryMaterialReferencedDesign = allDesigns.Where(x => x.design_id == ingItemEnum.Current.PastryMaterials.design_id).First(); }
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

                    GetUsedItemsByOccurence? currentRecord = response.Find(x => x.itemId == ingItemEnum.Current.item_id);

                    if (currentRecord == null)
                    {
                        GetUsedItemsByOccurence newEntry = new GetUsedItemsByOccurence()
                        {
                            itemId = ingItemEnum.Current.item_id,
                            itemName = currentItemName,
                            itemType = ingItemEnum.Current.ingredient_type,
                            asMaterialIngredient = new List<string>(),
                            asCakeIngredient = new List<string>(),
                            numOfUsesCakeIngredient = 0,
                            numOfUsesMaterialIngredient = 0,
                            ratioOfUsesCakeIngredient = 0.0,
                            ratioOfUsesMaterialIngredient = 0.0
                        };

                        response.Add(newEntry);
                        currentRecord = response.Find(x => x.itemId == ingItemEnum.Current.item_id);
                    }
                    totalNumberOfPastryIngredients += 1;
                    currentRecord.numOfUsesCakeIngredient += 1;
                    currentRecord.asCakeIngredient.Add(ingItemEnum.Current.PastryMaterials.pastry_material_id + ": " + " " + currentParentPastryMaterialReferencedDesign.display_name);
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

                    GetUsedItemsByOccurence? currentRecord = response.Find(x => x.itemId == matIngItemEnum.Current.item_id);
                    if (currentRecord == null)
                    {
                        GetUsedItemsByOccurence newEntry = new GetUsedItemsByOccurence()
                        {
                            itemId = matIngItemEnum.Current.item_id,
                            itemName = currentItemName,
                            itemType = matIngItemEnum.Current.ingredient_type,
                            asMaterialIngredient = new List<string>(),
                            asCakeIngredient = new List<string>(),
                            numOfUsesCakeIngredient = 0,
                            numOfUsesMaterialIngredient = 0,
                            ratioOfUsesCakeIngredient = 0.0,
                            ratioOfUsesMaterialIngredient = 0.0
                        };

                        response.Add(newEntry);
                        currentRecord = response.Find(x => x.itemId == matIngItemEnum.Current.item_id);
                    }
                    totalNumberOfMaterialIngredients += 1;
                    currentRecord.numOfUsesMaterialIngredient += 1;
                    currentRecord.asMaterialIngredient.Add(matIngItemEnum.Current.Materials.material_id + ": " + matIngItemEnum.Current.Materials.material_name);
                }
            }
            //Ratio calculation
            foreach (GetUsedItemsByOccurence currentResponseRow in response)
            {
                double curRowNumOfUsesCakeIng = currentResponseRow.numOfUsesCakeIngredient;
                double curRowNumOfUsesMatIng = currentResponseRow.numOfUsesMaterialIngredient;

                currentResponseRow.ratioOfUsesCakeIngredient = curRowNumOfUsesCakeIng <= 0 ? 0 : curRowNumOfUsesCakeIng / totalNumberOfPastryIngredients;
                currentResponseRow.ratioOfUsesMaterialIngredient = curRowNumOfUsesMatIng <= 0 ? 0 : curRowNumOfUsesMatIng / totalNumberOfMaterialIngredients;
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
                                response.Sort((x, y) => y.itemId.CompareTo(x.itemId));
                                break;
                            default:
                                response.Sort((x, y) => x.itemId.CompareTo(y.itemId));
                                break;
                        }
                        break;
                    case "item_name":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.itemName.CompareTo(x.itemName));
                                break;
                            default:
                                response.Sort((x, y) => x.itemName.CompareTo(y.itemName));
                                break;
                        }
                        break;
                    case "item_type":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.itemType.CompareTo(x.itemType));
                                break;
                            default:
                                response.Sort((x, y) => x.itemType.CompareTo(y.itemType));
                                break;
                        }
                        break;
                    case "num_of_uses_cake_ingredient":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.numOfUsesCakeIngredient.CompareTo(x.numOfUsesCakeIngredient));
                                break;
                            default:
                                response.Sort((x, y) => x.numOfUsesCakeIngredient.CompareTo(y.numOfUsesCakeIngredient));
                                break;
                        }
                        break;
                    case "num_of_uses_material_ingredient":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.numOfUsesMaterialIngredient.CompareTo(x.numOfUsesMaterialIngredient));
                                break;
                            default:
                                response.Sort((x, y) => x.numOfUsesMaterialIngredient.CompareTo(y.numOfUsesMaterialIngredient));
                                break;
                        }
                        break;
                    case "ratio_of_uses_material_ingredient":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.ratioOfUsesMaterialIngredient.CompareTo(x.ratioOfUsesMaterialIngredient));
                                break;
                            default:
                                response.Sort((x, y) => x.ratioOfUsesMaterialIngredient.CompareTo(y.ratioOfUsesMaterialIngredient));
                                break;
                        }
                        break;
                    case "ratio_of_uses_cake_ingredient":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.ratioOfUsesCakeIngredient.CompareTo(x.ratioOfUsesCakeIngredient));
                                break;
                            default:
                                response.Sort((x, y) => x.ratioOfUsesCakeIngredient.CompareTo(y.ratioOfUsesCakeIngredient));
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

            List<PastryMaterials> allPastryMaterials = await _context.PastryMaterials.Where(x => x.is_active == true).ToListAsync();
            if (allPastryMaterials.IsNullOrEmpty()) { return new List<GetUsedItemsBySeasonalTrends>(); }

            List<Item> allInventoryItems = await _kaizenTables.Item.Where(x => x.is_active == true).ToListAsync();
            if (allInventoryItems.IsNullOrEmpty()) { return new List<GetUsedItemsBySeasonalTrends>(); }

            List<Ingredients> ingredientsItems = await _context.Ingredients.Where(row => row.is_active == true).Select(row => new Ingredients() { item_id = row.item_id, ingredient_type = row.ingredient_type, PastryMaterials = row.PastryMaterials }).ToListAsync();
            List<MaterialIngredients> materialIngredientsItems = await _context.MaterialIngredients.Where(x => x.Materials.is_active == true && x.is_active == true).ToListAsync();
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

                newResponseEntry.dateStart = new DateTime(currentDateYear, currentDateMonth, 1);
                newResponseEntry.dateEnd = new DateTime(currentDateYear, currentDateMonth, DateTime.DaysInMonth(currentDateYear, currentDateMonth));

                newResponseEntry.itemList = new List<ItemOccurence>();

                List<Orders> ordersForCurrentDate = ordersList.Where(x => x.created_at >= newResponseEntry.dateStart && x.created_at <= newResponseEntry.dateEnd).ToList();

                //Add all items in the list of occurence for the current month
                foreach (Orders currentOrder in ordersForCurrentDate)
                {
                    DateTime currentOrderCreationDate = currentOrder.created_at;
                    Guid currentOrderDesignId = currentOrder.design_id;

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

                                    ItemOccurence? currentOccurenceEntry = newResponseEntry.itemList.Find(x => x.itemId == i.item_id);
                                    if (currentOccurenceEntry == null)
                                    {
                                        newResponseEntry.itemList.Add(new ItemOccurence()
                                        {
                                            itemId = i.item_id,
                                            itemName = currentInventoryItem.item_name, //Add code here to find the item name in inventory
                                            itemType = i.ingredient_type,
                                            occurrenceCount = 1
                                        });
                                    }
                                    else { currentOccurenceEntry.occurrenceCount += 1; }

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

                                        ItemOccurence? currentOccurenceEntry = newResponseEntry.itemList.Find(x => x.itemId == currentMaterialIngredient.item_id);

                                        if (currentOccurenceEntry == null)
                                        {
                                            newResponseEntry.itemList.Add(new ItemOccurence()
                                            {
                                                itemId = currentMaterialIngredient.item_id,
                                                itemName = currentInventoryItem.item_name, //Add code here to find the item name in inventory
                                                itemType = currentMaterialIngredient.ingredient_type,
                                                occurrenceCount = 1
                                            });
                                        }
                                        else { currentOccurenceEntry.occurrenceCount += 1; }
                                        totalNumberOfIngredientsInInterval += 1;
                                    }
                                    break;
                                }
                        }
                    }
                }
                //Calculate the ratio for the ingredients in the occurence list
                foreach (ItemOccurence currentItemForRatioCalculation in newResponseEntry.itemList)
                {
                    currentItemForRatioCalculation.ratio = currentItemForRatioCalculation.occurrenceCount / totalNumberOfIngredientsInInterval;
                }
            }
            await _actionLogger.LogAction(User, "GET", "All items by seasonal occurence");
            return response;
        }

        [HttpGet("tags-used/occurrence/")]
        public async Task<List<GetTagOccurrence>> GetTagOccurrence(string? sortBy, string? sortOrder)
        {
            List<DesignTags> allTags = await _context.DesignTags.Where(x => x.is_active == true).ToListAsync();
            if (allTags.IsNullOrEmpty()) { return new List<GetTagOccurrence>(); }
            List<DesignTagsForCakes> allTagsForCake = await _context.DesignTagsForCakes.Where(x => x.is_active == true).ToListAsync();

            List<GetTagOccurrence> response = new List<GetTagOccurrence>();

            foreach (DesignTagsForCakes DesignTagsForCakes in allTagsForCake)
            {
                DesignTags? selectedTag = allTags.Where(x => x.design_tag_id == DesignTagsForCakes.design_tag_id).FirstOrDefault();
                if (selectedTag == null) { continue; }
                GetTagOccurrence? selectedResponseRow = response.Where(x => x.designTagId == selectedTag.design_tag_id).FirstOrDefault();
                if (selectedResponseRow != null) { selectedResponseRow.occurrenceCount += 1; }
                else
                {
                    GetTagOccurrence newResponseEntry = new GetTagOccurrence();
                    newResponseEntry.designTagId = selectedTag.design_tag_id;
                    newResponseEntry.designTagName = selectedTag.design_tag_name;
                    newResponseEntry.occurrenceCount = 1;
                    newResponseEntry.ratio = 0.0;
                    response.Add(newResponseEntry);
                }
            }
            double totalAmountOfCakeTags = Convert.ToDouble(allTagsForCake.Count());
            foreach (GetTagOccurrence currentResponseRow in response)
            {
                currentResponseRow.ratio = currentResponseRow.occurrenceCount / totalAmountOfCakeTags;
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
                                response.Sort((x, y) => y.designTagName.CompareTo(x.designTagName));
                                break;
                            default:
                                response.Sort((x, y) => x.designTagName.CompareTo(y.designTagName));
                                break;
                        }
                        break;
                    case "occurrence_count":
                        switch (sortOrder)
                        {
                            case "DESC":
                                response.Sort((x, y) => y.occurrenceCount.CompareTo(x.occurrenceCount));
                                break;
                            default:
                                response.Sort((x, y) => x.occurrenceCount.CompareTo(y.occurrenceCount));
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
                                response.Sort((x, y) => y.designTagId.CompareTo(x.designTagId));
                                break;
                            default:
                                response.Sort((x, y) => x.designTagId.CompareTo(y.designTagId));
                                break;
                        }
                        break;
                }
            }

            await _actionLogger.LogAction(User, "GET", "Tag occurence");
            return response;
        }

        [HttpGet("ingredient-subtraction-history")]
        public async Task<List<GetIngredientSubtractionHistory>> GetIngredientSubtractionHistories(int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {
            List<IngredientSubtractionHistory> ingredientSubtractionHistories;
            List<GetIngredientSubtractionHistory> response = new List<GetIngredientSubtractionHistory>();

            IQueryable<IngredientSubtractionHistory> ingredientSubtractionHistoryQuery = _context.IngredientSubtractionHistory;
            //Row sorting algorithm
            if (sortBy != null)
            {
                sortOrder = sortOrder == null ? "ASC" : sortOrder.ToUpper() != "ASC" && sortOrder.ToUpper() != "DESC" ? "ASC" : sortOrder.ToUpper();

                switch (sortBy)
                {
                    case "date_subtracted":
                        switch (sortOrder)
                        {
                            case "DESC":
                                ingredientSubtractionHistoryQuery = ingredientSubtractionHistoryQuery.OrderByDescending(x => x.date_subtracted);
                                break;
                            default:
                                ingredientSubtractionHistoryQuery = ingredientSubtractionHistoryQuery.OrderBy(x => x.date_subtracted);
                                break;
                        }
                        break;
                    case "ingredient_subtraction_history_id":
                        switch (sortOrder)
                        {
                            case "DESC":
                                ingredientSubtractionHistoryQuery = ingredientSubtractionHistoryQuery.OrderByDescending(x => x.ingredient_subtraction_history_id);
                                break;
                            default:
                                ingredientSubtractionHistoryQuery = ingredientSubtractionHistoryQuery.OrderBy(x => x.ingredient_subtraction_history_id);
                                break;
                        }
                        break;
                    default:
                        switch (sortOrder)
                        {
                            case "DESC":
                                ingredientSubtractionHistoryQuery = ingredientSubtractionHistoryQuery.OrderByDescending(x => x.date_subtracted);
                                break;
                            default:
                                ingredientSubtractionHistoryQuery = ingredientSubtractionHistoryQuery.OrderBy(x => x.date_subtracted);
                                break;
                        }
                        break;
                }
            }
            //Paging algorithm
            if (page == null) { ingredientSubtractionHistories = await ingredientSubtractionHistoryQuery.ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < Page.DefaultNumberOfEntriesPerPage ? Page.DefaultNumberOfEntriesPerPage : record_per_page.Value;
                int current_page = page.Value < Page.DefaultStartingPageNumber ? Page.DefaultStartingPageNumber : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                ingredientSubtractionHistories = await ingredientSubtractionHistoryQuery.Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            foreach(IngredientSubtractionHistory currentIngredientSubtractionHistoryRow in  ingredientSubtractionHistories)
            {
                GetIngredientSubtractionHistory newResponseRow = await DataParser.CreateIngredientSubtractionHistoryResponseFromDBRow(currentIngredientSubtractionHistoryRow, _context);
                response.Add(newResponseRow);
            }

            return response;
        }
    }
    [ApiController]
    public class BOMDataManipulationController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        private readonly IActionLogger _actionLogger;

        public BOMDataManipulationController(DatabaseContext context, KaizenTables kaizen, IActionLogger logs) { _context = context; _actionLogger = logs; _kaizenTables = kaizen; }

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
                try { sub_variant = await _context.PastryMaterialSubVariants.Where(x => x.is_active == true && x.pastry_material_id == currentPastryMaterial.pastry_material_id && x.pastry_material_sub_variant_id.Equals(variant_id)).FirstAsync(); }
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

                    inventory_amount_unit = referencedInventoryItem.measurements,
                    inventory_price = referencedInventoryItem.price,
                    inventory_quantity = referencedInventoryItem.quantity,

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
                date_subtracted = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")),
            };
            await _context.IngredientSubtractionHistory.AddAsync(newIngredientSubtractionHistoryEntry);

            await _kaizenTables.SaveChangesAsync();
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Subtract ingredients of " + pastry_material_id);
            return Ok(new { message = "Ingredients sucessfully deducted." });
        }

        [HttpPost("orders/{order_id}/subtract-recipe-ingredients-on-inventory/")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> SubtractPastryMaterialIngredientsOnInventory(string order_id)
        {
            string decodedId = order_id;
            byte[]? byteArrEncodedId = null;
            try
            {
                Guid orderId = new Guid(order_id);
                byteArrEncodedId = orderId.ToByteArray();
            }
            catch { return BadRequest(new { message = "Invalid order id format on route" }); }

            Orders? selectedOrder = await _kaizenTables.Orders.Where(x => x.is_active == true && x.order_id.SequenceEqual(byteArrEncodedId)).FirstOrDefaultAsync();
            if (selectedOrder == null) { return NotFound(new { message = "No order with the specified id found" }); }
            OrderIngredientSubtractionLog? recordOfSubtraction = await _context.OrderIngredientSubtractionLog.Where(x => x.order_id.SequenceEqual(x.order_id)).FirstOrDefaultAsync();
            if (recordOfSubtraction != null) { return BadRequest(new { message = "Order " + order_id + " is already subtracted in the inventory" }); }

            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients
            Dictionary<string, InventorySubtractorInfo>? inventoryItemsAboutToBeSubtracted = null;

            try { inventoryItemsAboutToBeSubtracted = await DataParser.GetTotalIngredientAmountList(selectedOrder.pastry_id, _context, _kaizenTables); }
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

                    inventory_amount_unit = referencedInventoryItem.measurements,
                    inventory_price = referencedInventoryItem.price,
                    inventory_quantity = referencedInventoryItem.quantity,

                    amount_quantity_type = inventoryItemQuantityUnit,
                    amount_unit = referencedInventoryItem.measurements,
                    amount = amountToBeSubtracted
                };

                dataForSubtractionHistory.Add(newIngredientSubtractionInfoEntry);
                _kaizenTables.Item.Update(referencedInventoryItem);
            }
            Guid ingredientSubtractionHistoryRecordId = Guid.NewGuid();

            IngredientSubtractionHistory newIngredientSubtractionHistoryEntry = new IngredientSubtractionHistory
            {
                ingredient_subtraction_history_id = ingredientSubtractionHistoryRecordId,
                item_subtraction_info = dataForSubtractionHistory,
                date_subtracted = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")),
            };
            await _context.IngredientSubtractionHistory.AddAsync(newIngredientSubtractionHistoryEntry);
            await _context.SaveChangesAsync();

            OrderIngredientSubtractionLog newOrderSubtractionHistory = new OrderIngredientSubtractionLog
            {
                order_ingredient_subtraction_log_id = Guid.NewGuid(),
                order_id = byteArrEncodedId,
                ingredient_subtraction_history_id = ingredientSubtractionHistoryRecordId
            };
            await _context.OrderIngredientSubtractionLog.AddAsync(newOrderSubtractionHistory);
            await _context.SaveChangesAsync();

            await _kaizenTables.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Subtract ingredients of order:" + order_id);

            return Ok(new { message = "Ingredients sucessfully deducted." });
        }

        [HttpPost("custom-ingredient-subtraction")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> SubtractIngredientsFromForm(List<PostIngredients> data)
        {
            foreach (PostIngredients currentIngredient in data)
            {
                try { await DataVerification.IsIngredientItemValid(currentIngredient.itemId, currentIngredient.ingredientType, currentIngredient.amountMeasurement, _context, _kaizenTables); }
                catch (Exception e) { return BadRequest(new { message = e.Message }); }

            }
            
            List<ItemSubtractionInfo> dataForSubtractionHistory = new List<ItemSubtractionInfo>();
            foreach (PostIngredients currentIngredient in data)
            {
                Item referencedInventoryItem = await DataRetrieval.GetInventoryItemAsync(currentIngredient.itemId, _kaizenTables);

                ItemSubtractionInfo currentRecordForSubtractionHistory = new ItemSubtractionInfo
                {
                    item_id = currentIngredient.itemId,
                    item_name = referencedInventoryItem.item_name,

                    inventory_amount_unit = referencedInventoryItem.measurements,
                    inventory_price = referencedInventoryItem.price,
                    inventory_quantity = referencedInventoryItem.quantity,

                    amount_quantity_type = ValidUnits.UnitQuantityMeasurement(referencedInventoryItem.measurements),
                    amount_unit = currentIngredient.amountMeasurement,
                    amount = currentIngredient.amount
                };
                dataForSubtractionHistory.Add(currentRecordForSubtractionHistory);

                _kaizenTables.Item.Update(referencedInventoryItem);

                double amountToBeSubtracted = 0.0;
                if (currentRecordForSubtractionHistory.amount_quantity_type.Equals("Count"))
                {
                    amountToBeSubtracted = currentRecordForSubtractionHistory.amount;

                    referencedInventoryItem.quantity = referencedInventoryItem.quantity - currentRecordForSubtractionHistory.amount;
                }
                else
                {
                    amountToBeSubtracted = UnitConverter.ConvertByName(currentRecordForSubtractionHistory.amount, currentRecordForSubtractionHistory.amount_quantity_type, currentRecordForSubtractionHistory.amount_unit, referencedInventoryItem.measurements);

                    referencedInventoryItem.quantity = referencedInventoryItem.quantity - amountToBeSubtracted;
                }

                dataForSubtractionHistory.Add(currentRecordForSubtractionHistory);
            }


            IngredientSubtractionHistory newIngredientSubtractionHistoryEntry = new IngredientSubtractionHistory
            {
                ingredient_subtraction_history_id = Guid.NewGuid(),
                item_subtraction_info = dataForSubtractionHistory,
                date_subtracted = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")),
            };

            await _context.IngredientSubtractionHistory.AddAsync(newIngredientSubtractionHistoryEntry);
            await _kaizenTables.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Manual subtraction");
            return Ok(new { message = "Successfuly subtracted ingredients!" });
        }
    }
}
