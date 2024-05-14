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

namespace API_TEST.Controllers
{
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

        [HttpGet("TEST")]
        public async Task<List<MaterialIngredients>> test(string material_id)
        {
            List<MaterialIngredients> currentMaterialIngredients = _context.MaterialIngredients.Where(x => x.material_id == material_id).ToList();

            int currentIndex = 0;
            bool running = true;
            while (running)
            {
                MaterialIngredients? currentMatIngInLoop = null;
                try { currentMatIngInLoop = currentMaterialIngredients.ElementAt(currentIndex); }
                catch { running = false; break; }

                if (currentMatIngInLoop.ingredient_type == IngredientType.Material)
                {
                    List<MaterialIngredients> newEntriesToLoopThru = await _context.MaterialIngredients.Where(x => x.material_id == currentMatIngInLoop.item_id).ToListAsync();

                    currentMaterialIngredients.AddRange(newEntriesToLoopThru);
                }
                currentIndex += 1;
            }
            return currentMaterialIngredients;
        }
        
        [HttpPost("ADDMOCKDATA")]
        public async Task<IActionResult> AddMockData()
        {
            List<PastryMaterials> pEntries = new List<PastryMaterials>();
            List<Ingredients> pmEntries = new List<Ingredients>();

            List<Materials> mEntries = new List<Materials>();
            List<MaterialIngredients> miEntries = new List<MaterialIngredients>();

            List<Item> kiEntries = new List<Item>();
            List<Orders> koEntries = new List<Orders>();

            pEntries.Add(new PastryMaterials { DesignId = "1231", pastry_material_id = "PMID000000000001", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now });
            pEntries.Add(new PastryMaterials { DesignId = "1232", pastry_material_id = "PMID000000000002", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now });
            pEntries.Add(new PastryMaterials { DesignId = "1233", pastry_material_id = "PMID000000000003", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now });
            pEntries.Add(new PastryMaterials { DesignId = "1234", pastry_material_id = "PMID000000000004", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now });
            pEntries.Add(new PastryMaterials { DesignId = "1235", pastry_material_id = "PMID000000000005", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now });

            pmEntries.Add(new Ingredients { pastry_material_id = "PMID000000000001", ingredient_id = "IID000000000001", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now, amount = 12, amount_measurement = "grams", ingredient_type = "INV", item_id = "2231" });
            pmEntries.Add(new Ingredients { pastry_material_id = "PMID000000000002", ingredient_id = "IID000000000002", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now, amount = 15, amount_measurement = "grams", ingredient_type = "INV", item_id = "2231" });
            pmEntries.Add(new Ingredients { pastry_material_id = "PMID000000000003", ingredient_id = "IID000000000003", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now, amount = 100, amount_measurement = "milliliters", ingredient_type = "INV", item_id = "2233" });
            pmEntries.Add(new Ingredients { pastry_material_id = "PMID000000000004", ingredient_id = "IID000000000004", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now, amount = 75, amount_measurement = "grams", ingredient_type = "INV", item_id = "2232" });
            pmEntries.Add(new Ingredients { pastry_material_id = "PMID000000000005", ingredient_id = "IID000000000005", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now, amount = 1, amount_measurement = "piece", ingredient_type = "MAT", item_id = "MID000000000001" });

            kiEntries.Add(new Item { id = 2231, price = 100, quantity = 15, status = "NORMAL", isActive = true, item_name = "Butter", type = "LIQUID", created_at = DateTime.Now, last_updated_at = DateTime.Now, last_updated_by = "ME!" });
            kiEntries.Add(new Item { id = 2232, price = 10, quantity = 500, status = "NORMAL", isActive = true, item_name = "Sugar", type = "SOLID", created_at = DateTime.Now, last_updated_at = DateTime.Now, last_updated_by = "ME!" });
            kiEntries.Add(new Item { id = 2233, price = 24, quantity = 40, status = "NORMAL", isActive = true, item_name = "Milk", type = "LIQUID", created_at = DateTime.Now, last_updated_at = DateTime.Now, last_updated_by = "ME!" });
            kiEntries.Add(new Item { id = 2234, price = 67, quantity = 10, status = "NORMAL", isActive = true, item_name = "Flour", type = "SOLID", created_at = DateTime.Now, last_updated_at = DateTime.Now, last_updated_by = "ME!" });
            kiEntries.Add(new Item { id = 2235, price = 15, quantity = 105, status = "NORMAL", isActive = true, item_name = "Eggs", type = "SOLID", created_at = DateTime.Now, last_updated_at = DateTime.Now, last_updated_by = "ME!" });

            mEntries.Add(new Materials { material_id = "MID000000000001", material_name = "Chocolate", amount = 100, amount_measurement = "milliliters", isActive = true, date_added = DateTime.Now, last_modified_date = DateTime.Now });

            miEntries.Add(new MaterialIngredients { Materials = mEntries[0], material_id = "MID000000000001", material_ingredient_id = "MIID000000000001", item_id = "2231", amount = 2, amount_measurement = "pieces", ingredient_type = "INV", date_added = DateTime.Now, last_modified_date = DateTime.Now, isActive = true });
            miEntries.Add(new MaterialIngredients { Materials = mEntries[0], material_id = "MID000000000001", material_ingredient_id = "MIID000000000002", item_id = "2232", amount = 20, amount_measurement = "grams", ingredient_type = "INV", date_added = DateTime.Now, last_modified_date = DateTime.Now, isActive = true });
            miEntries.Add(new MaterialIngredients { Materials = mEntries[0], material_id = "MID000000000001", material_ingredient_id = "MIID000000000003", item_id = "2235", amount = 2, amount_measurement = "pieces", ingredient_type = "INV", date_added = DateTime.Now, last_modified_date = DateTime.Now, isActive = true });



            _context.PastryMaterials.AddRange(pEntries);
            _context.Materials.AddRange(mEntries);
            _context.SaveChanges();

            _context.Ingredients.AddRange(pmEntries);
            _context.MaterialIngredients.AddRange(miEntries);
            _context.SaveChanges();

            _kaizenTables.Item.AddRange(kiEntries);
            _kaizenTables.Orders.AddRange(koEntries);
            _kaizenTables.SaveChanges();

            return Ok();
        }
        
    }

    [ApiController]
    [Route("BOM/data_analysis")]
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
        [HttpGet("item_used/occurence/")]
        public async Task<List<GetUsedItemsByOccurence>> GetMostCommonlyUsedItems(string? sortBy, string? sortOrder)
        {
            List<Ingredients> ingredientsItems = _context.Ingredients.Where(row => row.isActive == true).Select(row => new Ingredients() { item_id = row.item_id, ingredient_type = row.ingredient_type, PastryMaterials = row.PastryMaterials }).ToList();
            List<MaterialIngredients> materialIngredientsItems = _context.MaterialIngredients.Where(row => row.isActive == true).Select(row => new MaterialIngredients() { item_id = row.item_id, ingredient_type = row.ingredient_type, Materials = row.Materials }).ToList();

            //Lists for checking if records referenced by ingredients and material_ingredients are active are active
            List<Materials> activeMaterials = await _context.Materials.Where(x => x.isActive == true).Select(x => new Materials() { material_id = x.material_id, material_name = x.material_name }).ToListAsync();
            List<Item> activeInventoryItems = await _kaizenTables.Item.Where(x => x.isActive == true).ToListAsync();  //Replace with function to get all active inventory items 

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
                    currentRecord.as_cake_ingredient.Add(ingItemEnum.Current.PastryMaterials.pastry_material_id + ": " + " <design_name_goes_here>");
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

                currentResponseRow.ratio_of_uses_cake_ingredient = curRowNumOfUsesCakeIng <= 0 ? 0 : curRowNumOfUsesCakeIng /totalNumberOfPastryIngredients;
                currentResponseRow.ratio_of_uses_material_ingredient = curRowNumOfUsesMatIng <= 0 ? 0 : curRowNumOfUsesMatIng / totalNumberOfMaterialIngredients;
            }
            //Sorting Algorithm
            if (sortBy != null)
            {

                sortOrder = sortOrder == null ? "ASC" : sortOrder.ToUpper() != "ASC" && sortOrder.ToUpper() != "DESC" ? "ASC": sortOrder.ToUpper();

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
        [HttpGet("item_used/seasonal_occurence")]
        public async Task<List<GetUsedItemsBySeasonalTrends>> GetIngredientTrendsByMonths(string? sortBy, string? sortOrder, bool? ingredientsOnly)
        {
            List<Orders> ordersList = await _kaizenTables.Orders.Where(x => x.is_active == true).ToListAsync();
            List<Item> allInventoryItems = await _kaizenTables.Item.Where(x => x.isActive == true).ToListAsync();

            List<Ingredients> ingredientsItems = await _context.Ingredients.Where(row => row.isActive == true).Select(row => new Ingredients() { item_id = row.item_id, ingredient_type = row.ingredient_type, PastryMaterials = row.PastryMaterials }).ToListAsync();
            List<MaterialIngredients> materialIngredientsItems = await _context.MaterialIngredients.Where(x => x.Materials.isActive == true && x.isActive == true).ToListAsync();
            List<PastryMaterials> allPastryMaterials = await _context.PastryMaterials.Where(x => x.isActive == true).ToListAsync();


            if (ordersList.IsNullOrEmpty()) { return new List<GetUsedItemsBySeasonalTrends>(); }
            if (allInventoryItems.IsNullOrEmpty()) { return new List<GetUsedItemsBySeasonalTrends>(); }

            if (materialIngredientsItems.IsNullOrEmpty() && ingredientsItems.IsNullOrEmpty()) { return new List<GetUsedItemsBySeasonalTrends>(); }
            if (allPastryMaterials.IsNullOrEmpty()) { return new List<GetUsedItemsBySeasonalTrends>(); }

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
                    string currentOrderDesignId = Encoding.UTF8.GetString(currentOrder.design_id);

                    PastryMaterials? currentOrderDesignRow = allPastryMaterials.Find(x => x.DesignId == currentOrderDesignId);
                    if (currentOrderDesignRow != null) { continue; }

                    List<Ingredients> currentOrderCakeIngredients = ingredientsItems.Where(x => x.pastry_material_id == currentOrderDesignId).ToList();
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
                                        occurence_count = 1
                                    });
                                }
                                else { currentOccurenceEntry.occurence_count += 1; }

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
                                            occurence_count = 1
                                        });
                                    }
                                    else { currentOccurenceEntry.occurence_count += 1; }
                                    totalNumberOfIngredientsInInterval += 1;
                                }
                                break;
                            }
                        }
                    }
                }
                //Calculate the ratio for the ingredients in the occurence list

            }

            //What to do here
            //1. Create new GetUsedItemsBySeasonalTrends item to be inserted in response list for the specified time period
            //2. Loop through all orders 
            //3. Check what cake it is, get all ingredients for it
            //4. Loop thru all ingredients retrieved, add or increase their count in the occurenceCount 
            //5. Calculate the ratio for each ingredient, add them in the list of the new entry in the response list

            await _actionLogger.LogAction(User, "GET", "All items by seasonal occurence");
            return response;
        }
    }

}
