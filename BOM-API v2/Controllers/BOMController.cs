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
    [Route("Debug/")]
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
}
