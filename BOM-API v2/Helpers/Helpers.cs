using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using UnitsNet;

namespace BillOfMaterialsAPI.Helpers
{
    //Loosely Coupled
    public static class IdPrefix
    {
        public const string Material = "MID";
        public const string MaterialIngredient = "MIID";

        public const string PastryMaterial = "PMID";
        public const string Ingredient = "IID";
        public const string PastryMaterialAddOn = "PMAOID";
        public const string PastryMaterialSubVariant = "SVID";
        public const string PastryMaterialSubVariantIngredient = "SVIID";
        public const string PastryMaterialSubVariantAddOn = "SVAOID";

        public const string Logs = "LOG";
    }
    public static class PastryMaterialIngredientImportanceCode
    {
        public const int Critical = 5;
        public const int High = 4;
        public const int Normal = 3;
        public const int Low = 2;
        public const int VeryLow = 1;

        public static Dictionary<string, int> ValidIngredientImportanceCodes()
        {
            Dictionary<string, int> response = new Dictionary<string, int>();

            response.Add("Critical", PastryMaterialIngredientImportanceCode.Critical);
            response.Add("High", PastryMaterialIngredientImportanceCode.High);
            response.Add("Normal", PastryMaterialIngredientImportanceCode.Normal);
            response.Add("Low", PastryMaterialIngredientImportanceCode.Low);
            response.Add("VeryLow", PastryMaterialIngredientImportanceCode.VeryLow);

            return response;
        }
    }
    public static class PastryMaterialRecipeStatus
    {
        public const int AllIngredientsAvailable = 1;

        public const int OneIngredientOutOfStock = 2;
        public const int TwoOrMoreIngredientsOutOfStock = 3;

        public const int OneCriticalImportanceIngredientLowOnStock = 4;
        public const int TwoOrMoreCriticalImportanceIngredientsLowOnStock = 5;

        public const int OneHighImportanceIngredientLowOnStock = 6;
        public const int TwoOrMoreHighImportanceIngredientsLowOnStock = 7;

        public const int OneNormalImportanceIngredientLowOnStock = 8;
        public const int TwoOrMoreNormalImportanceIngredientsLowOnStock = 9;

        public const int OneLowImportanceIngredientLowOnStock = 10;
        public const int TwoOrMoreLowImportanceIngredientsLowOnStock = 11;

        public const int OneVeryLowImportanceIngredientLowOnStock = 12;
        public const int TwoOrMoreVeryLowImportanceIngredientsLowOnStock = 13;

    }
    public class Page
    {
        public static int DefaultStartingPageNumber = 1;
        public static int DefaultNumberOfEntriesPerPage = 10;
    }
    public class Iterators
    {
        public static IEnumerable<DateTime> LoopThroughMonths(DateTime start, DateTime end)
        {
            DateTime startDate = new DateTime(start.Year, start.Month, 1);
            DateTime endDate = new DateTime(end.Year, end.Month, 1);

            for (DateTime i = startDate; i <= endDate; i.AddMonths(1)) yield return i;
        }
    }

    //Tightly Coupled
    public class InventorySubtractorInfo
    {

        public string AmountQuantityType { get; set; }
        public string AmountUnit { get; set; }
        public double Amount { get; set; }

        public InventorySubtractorInfo() { }
        public InventorySubtractorInfo(string amountQuantityType, string amountUnit, double amount)
        {
            this.AmountQuantityType = amountQuantityType;
            this.AmountUnit = amountUnit;
            this.Amount = amount;
        }
    }
    public class IdFormat
    {
        public const int IdNumbersLength = 12;

        public static string IncrementId(string idStringBuffer, int idNumberLength, string idString)
        {
            int index = idString.IndexOf(idStringBuffer);
            string idNumeralsPart = (index < 0) ? idString : idString.Remove(index, idStringBuffer.Length);
            int idInt = Convert.ToInt32(idNumeralsPart);

            int newIdInt = idInt + 1;
            int numberOfNumerals = Convert.ToInt32(newIdInt.ToString()).ToString().Length;

            string newId = newIdInt.ToString();
            for (int i = 0; i < idNumberLength - numberOfNumerals; i++)
            {
                newId = "0" + newId;
            }
            newId = idStringBuffer + newId;
            return newId;
        }

        public static async Task<string> GetNewestPastryMaterialId(DatabaseContext context)
        {
            string lastPastryMaterialId = "";
            string newPastryMaterialId = "";

            try { PastryMaterials x = await context.PastryMaterials.OrderByDescending(x => x.pastry_material_id).FirstAsync(); lastPastryMaterialId = x.pastry_material_id; }
            catch (Exception ex)
            {
                newPastryMaterialId = IdPrefix.PastryMaterial;
                for (int i = 1; i <= IdFormat.IdNumbersLength; i++) { newPastryMaterialId += "0"; }
                lastPastryMaterialId = newPastryMaterialId;
            }
            newPastryMaterialId = IdFormat.IncrementId(IdPrefix.PastryMaterial, IdFormat.IdNumbersLength, lastPastryMaterialId);

            return newPastryMaterialId;
        }
        public static async Task<string> GetNewestIngredientId(DatabaseContext context)
        {
            string lastIngredientId = "";
            string newIngredientId = "";
            try { Ingredients x = await context.Ingredients.OrderByDescending(x => x.ingredient_id).FirstAsync(); lastIngredientId = x.ingredient_id; }
            catch (Exception ex)
            {
                newIngredientId = IdPrefix.Ingredient;
                for (int i = 1; i <= IdFormat.IdNumbersLength; i++) { newIngredientId += "0"; }
                lastIngredientId = newIngredientId;
            }
            newIngredientId = IdFormat.IncrementId(IdPrefix.Ingredient, IdFormat.IdNumbersLength, lastIngredientId);
            return newIngredientId;
        }
        public static async Task<string> GetNewestPastryMaterialAddOnId(DatabaseContext context)
        {
            string lastPastryMaterialAddOnId = "";
            string newPastryMaterialAddOnId = "";

            try { PastryMaterialAddOns x = await context.PastryMaterialAddOns.OrderByDescending(x => x.pastry_material_add_on_id).FirstAsync(); lastPastryMaterialAddOnId = x.pastry_material_add_on_id; }
            catch (Exception ex)
            {
                newPastryMaterialAddOnId = IdPrefix.PastryMaterialAddOn;
                for (int i = 1; i <= IdFormat.IdNumbersLength; i++) { newPastryMaterialAddOnId += "0"; }
                lastPastryMaterialAddOnId = newPastryMaterialAddOnId;
            }
            newPastryMaterialAddOnId = IdFormat.IncrementId(IdPrefix.PastryMaterialAddOn, IdFormat.IdNumbersLength, lastPastryMaterialAddOnId);
            return newPastryMaterialAddOnId;
        }

        public static async Task<string> GetNewestPastryMaterialSubVariantId(DatabaseContext context)
        {
            string lastPastryMaterialSubVariantId = "";
            string newPastryMaterialSubVariantId = "";

            try { PastryMaterialSubVariants x = await context.PastryMaterialSubVariants.OrderByDescending(x => x.pastry_material_sub_variant_id).FirstAsync(); lastPastryMaterialSubVariantId = x.pastry_material_sub_variant_id; }
            catch (Exception ex)
            {
                newPastryMaterialSubVariantId = IdPrefix.PastryMaterialSubVariant;
                for (int i = 1; i <= IdFormat.IdNumbersLength; i++) { newPastryMaterialSubVariantId += "0"; }
                lastPastryMaterialSubVariantId = newPastryMaterialSubVariantId;
            }
            newPastryMaterialSubVariantId = IdFormat.IncrementId(IdPrefix.PastryMaterialSubVariant, IdFormat.IdNumbersLength, lastPastryMaterialSubVariantId);
            return newPastryMaterialSubVariantId;
        }
        public static async Task<string> GetNewestPastryMaterialSubVariantIngredientId(DatabaseContext context)
        {
            string lastSubVariantIngredientId = "";
            string newSubVariantIngredientId = "";

            try { PastryMaterialSubVariantIngredients x = await context.PastryMaterialSubVariantIngredients.OrderByDescending(x => x.pastry_material_sub_variant_ingredient_id).FirstAsync(); lastSubVariantIngredientId = x.pastry_material_sub_variant_ingredient_id; }
            catch (Exception ex)
            {
                newSubVariantIngredientId = IdPrefix.PastryMaterialSubVariantIngredient;
                for (int i = 1; i <= IdFormat.IdNumbersLength; i++) { newSubVariantIngredientId += "0"; }
                lastSubVariantIngredientId = newSubVariantIngredientId;
            }
            newSubVariantIngredientId = IdFormat.IncrementId(IdPrefix.PastryMaterialSubVariantIngredient, IdFormat.IdNumbersLength, lastSubVariantIngredientId);
            return newSubVariantIngredientId;
        }
        public static async Task<string> GetNewestPastryMaterialSubVariantAddOnId(DatabaseContext context)
        {
            string lastSubVariantAddOnId = "";
            string newSubVariantAddOnId = "";

            try { PastryMaterialSubVariantAddOns x = await context.PastryMaterialSubVariantAddOns.OrderByDescending(x => x.pastry_material_sub_variant_add_on_id).FirstAsync(); lastSubVariantAddOnId = x.pastry_material_sub_variant_add_on_id; }
            catch (Exception ex)
            {
                newSubVariantAddOnId = IdPrefix.PastryMaterialSubVariantAddOn;
                for (int i = 1; i <= IdFormat.IdNumbersLength; i++) { newSubVariantAddOnId += "0"; }
                lastSubVariantAddOnId = newSubVariantAddOnId;
            }
            newSubVariantAddOnId = IdFormat.IncrementId(IdPrefix.PastryMaterialSubVariantAddOn, IdFormat.IdNumbersLength, lastSubVariantAddOnId);
            return newSubVariantAddOnId;
        }
    }

    public class ValidUnits
    {
        public static Dictionary<string, List<string>> ValidMeasurementUnits()
        {
            Dictionary<string, List<string>> response = new Dictionary<string, List<string>>();

            string[] validQuantities = ["Mass", "Volume"];
            foreach (string currentQuantity in validQuantities)
            {
                List<string> currentQuantityUnits = new List<string>();
                foreach (UnitInfo currentUnit in Quantity.ByName[currentQuantity].UnitInfos)
                {
                    currentQuantityUnits.Add(currentUnit.Name);
                }
                response.Add(currentQuantity, currentQuantityUnits);
            }
            response.Add("Count", new List<string> { "Piece" });

            return response;
        }
        public static bool IsSameQuantityUnit(string x, string y)
        {
            Dictionary<string, List<string>> response = new Dictionary<string, List<string>>();

            string[] validQuantities = ["Mass", "Volume", "Count"];
            foreach (string currentQuantity in validQuantities)
            {
                if (currentQuantity.Equals("Count") == false)
                {
                    bool doesXExistInCurrentQuantityUnit = false;
                    bool doesYExistInCurrentQuantityUnit = false;

                    foreach (UnitInfo currentUnit in Quantity.ByName[currentQuantity].UnitInfos)
                    {
                        if (currentUnit.Name.Equals(x)) { doesXExistInCurrentQuantityUnit = true; }
                        if (currentUnit.Name.Equals(y)) { doesYExistInCurrentQuantityUnit = true; }
                        if (doesXExistInCurrentQuantityUnit == true && doesYExistInCurrentQuantityUnit == true) { break; }
                    }
                    if (doesXExistInCurrentQuantityUnit == true && doesYExistInCurrentQuantityUnit == true) { return true; }
                }
                else
                {
                    string validMeasurement = "Piece";
                    if (x.Equals(validMeasurement) && y.Equals(validMeasurement)) { return true; }
                }
            }
            return false;
        }
        public static bool IsUnitValid(string x)
        {
            Dictionary<string, List<string>> validUnitList = ValidMeasurementUnits();

            foreach (string quantity in validUnitList.Keys)
            {
                List<string> units = validUnitList[quantity];
                if (units.Contains(x)) { return true; }
                else { continue; }
            }
            return false;
        }
        public static string UnitQuantityMeasurement(string x)
        {
            Dictionary<string, List<string>> validUnitList = ValidMeasurementUnits();

            foreach (string quantity in validUnitList.Keys)
            {
                List<string> units = validUnitList[quantity];
                if (units.Contains(x)) { return quantity; }
                else { continue; }
            }
            return "";
        }
    }
    public class DataVerification
    {
        public static async Task<bool> PastryMaterialExistsAsync(string pastry_material_id, DatabaseContext context)
        {
            PastryMaterials? currentPastryMaterial;
            try
            {
                currentPastryMaterial = await context.PastryMaterials.Where(x
                => x.is_active == true).FirstAsync(x => x.pastry_material_id == pastry_material_id);

                return true;
            }
            catch { }
            return false; //throw new NotFoundInDatabaseException("No Pastry Material with the specified id found.");
        }
        public static async Task<bool> PastryMaterialIngredientExistsAsync(string ingredient_id, DatabaseContext context)
        {
            Ingredients? currentIngredient;
            try
            {
                currentIngredient = await context.Ingredients.Where(x
                => x.is_active == true && x.ingredient_id == ingredient_id).FirstAsync();

                return true;
            }
            catch { }
            return false;
        }
        public static async Task<bool> PastryMaterialIngredientExistsAsync(string pastry_material_id, string ingredient_id, DatabaseContext context)
        {
            Ingredients? currentIngredient;
            try
            {
                currentIngredient = await context.Ingredients.Where(x
                => x.is_active == true && x.ingredient_id == ingredient_id && x.pastry_material_id == pastry_material_id).FirstAsync();

                return true;
            }
            catch { }
            return false;
        }

        public static async Task<bool> DesignExistsAsync(byte[] designId, DatabaseContext context)
        {
            Designs? selectedDesign;
            try
            {
                selectedDesign = await context.Designs.Where(x => x.is_active == true && x.design_id == designId).FirstAsync();
                return true;
            }
            catch { }
            return false;
        }
        public static async Task<bool> InventoryItemExistsAsync(string id, KaizenTables kaizenTables)
        {
            Item? currentInventoryItem;
            try
            {
                currentInventoryItem = await kaizenTables.Item.Where(x => x.is_active == true && x.id == Convert.ToInt32(id)).FirstAsync();
                return true;
            }
            catch (FormatException exF) { throw new FormatException("Invalid id format for " + id + ", must be a value that can be parsed as an integer."); }
            catch (InvalidOperationException exO) { return false; }
        }

        public static async Task<bool> IsIngredientItemValid(string item_id, string ingredient_type, string amount_measurement, DatabaseContext context, KaizenTables kaizenTables)
        {
            switch (ingredient_type)
            {
                case IngredientType.InventoryItem:
                    //!!!UNTESTED!!!
                    Item? currentInventoryItem = null;
                    try { currentInventoryItem = await DataRetrieval.GetInventoryItemAsync(item_id, kaizenTables); }
                    catch (FormatException exF) { throw; }
                    catch (NotFoundInDatabaseException exO) { throw; }

                    if (ValidUnits.IsSameQuantityUnit(currentInventoryItem.measurements, amount_measurement) == false) { throw new InvalidAmountMeasurementException("Ingredient with the inventory item id " + currentInventoryItem.id + " does not have the same quantity unit as the referred inventory item"); }
                    break;
                case IngredientType.Material:
                    //Check if item id exists on the 'Materials' table
                    //or in the inventory
                    Materials? currentReferredMaterial = null;
                    try { currentReferredMaterial = await context.Materials.Where(x => x.is_active == true && x.material_id == item_id).FirstAsync(); }
                    catch { throw new NotFoundInDatabaseException("Id specified in the request does not exist in the database. Id " + item_id); }
                    if (ValidUnits.IsSameQuantityUnit(currentReferredMaterial.amount_measurement, amount_measurement) == false) { throw new InvalidAmountMeasurementException("Ingredient with the material item id " + currentReferredMaterial.material_id + " does not have the same quantity unit as the referred material"); }
                    break;
                default:
                    throw new InvalidPastryMaterialIngredientTypeException("Ingredients to be inserted has an invalid ingredient_type, valid types are MAT and INV.");
            }
            return true;
        }
        public static async Task<bool> DoesIngredientExistsInPastryMaterial(string pastry_material_id, string item_id, string ingredient_type, DatabaseContext context)
        {
            bool response = false;

            PastryMaterials? currentPastryMaterial;
            try { currentPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(pastry_material_id, context); }
            catch { throw; }

            List<Ingredients> mainIngredients = await context.Ingredients.Where(x => x.is_active == true && x.pastry_material_id == pastry_material_id).ToListAsync();
            response =
                mainIngredients.Where(x => x.item_id == item_id && x.ingredient_type == ingredient_type).FirstOrDefault() != null;
            if (response == true) return response;

            List<PastryMaterialSubVariants> subVariants = await context.PastryMaterialSubVariants.Where(x => x.is_active == true && x.pastry_material_id == pastry_material_id).ToListAsync();
            foreach (PastryMaterialSubVariants variant in subVariants)
            {
                List<PastryMaterialSubVariantIngredients> subVariantIngredients = await context.PastryMaterialSubVariantIngredients.Where(x => x.is_active == true && x.pastry_material_sub_variant_id == variant.pastry_material_sub_variant_id).ToListAsync();

                response = subVariantIngredients.Where(x => x.item_id == item_id && x.ingredient_type == ingredient_type).FirstOrDefault() != null;

                if (response == true) return response;
            }

            return response;
        }

        //Used to check the status of the recipe
        public static async Task<int[]> PastryMaterialRecipeStatus(string variant_id, DatabaseContext context, KaizenTables kaizenTables)
        {
            int[] response = [];

            PastryMaterials? selectedPastryMaterial = null;
            PastryMaterialSubVariants? selectedPastryMaterialSubVariant = null;

            try { selectedPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(variant_id, context); }
            catch (NotFoundInDatabaseException e) { }
            try { selectedPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(variant_id, context); }
            catch (NotFoundInDatabaseException e) { }

            if (selectedPastryMaterial == null && selectedPastryMaterialSubVariant == null) { throw new NotFoundInDatabaseException(variant_id + " does not exist in both pastry material and the subvariant tables"); }
            if (selectedPastryMaterial == null && selectedPastryMaterialSubVariant != null) { selectedPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(selectedPastryMaterialSubVariant.pastry_material_id, context); }
            if (selectedPastryMaterial == null) { throw new NotFoundInDatabaseException(variant_id + " exist in subvariant table, but the base pastry material it points to does not exist"); }

            List<PastryMaterialIngredientImportance> ingredientImportanceList = await context.PastryMaterialIngredientImportance.Where(x => x.pastry_material_id == selectedPastryMaterial.pastry_material_id).ToListAsync();
            Dictionary<string, InventorySubtractorInfo> totalVariantIngredientConsumptionList = await DataParser.GetTotalIngredientAmountList(variant_id, context, kaizenTables);

            foreach (string currentIngredientId in totalVariantIngredientConsumptionList.Keys)
            {
                PastryMaterialIngredientImportance? currentIngredientImportance = ingredientImportanceList.Where(x => x.item_id == currentIngredientId).FirstOrDefault();

                //If record for ingredientImportance is not found; default to normal importance
                //Else; use the value in the found value


            }

            return response;
        }
    }
    public class DataInsertion
    {
        public static async Task<string> AddPastryMaterialIngredient(string pastry_material_id, PostIngredients data, DatabaseContext context)
        {
            string response = "";

            string lastPastryMaterialIngredientId = await IdFormat.GetNewestIngredientId(context);
            await TrackPastryMaterialIngredientForInsertion(lastPastryMaterialIngredientId, pastry_material_id, data, context);
            response = lastPastryMaterialIngredientId;

            return response;
        }
        public static async Task<Guid> AddPastryMaterialIngredientImportance(string pastry_material_id, PostPastryMaterialIngredientImportance data, DatabaseContext context)
        {
            Guid response;

            response = await TrackPastryMaterialIngredientImportanceForInsertion(pastry_material_id, data, context);

            return response;
        }
        public static async Task<string> AddPastryMaterialAddOns(string pastry_material_id, PostPastryMaterialAddOns data, DatabaseContext context)
        {
            string response = "";

            string lastPastryMaterialAddOnId = await IdFormat.GetNewestPastryMaterialAddOnId(context);
            await TrackPastryMaterialAddOnForInsertion(lastPastryMaterialAddOnId, pastry_material_id, data, context);
            response = lastPastryMaterialAddOnId;

            return response;
        }
        public static async Task<string> AddPastryMaterialSubVariantIngredient(string pastry_material_sub_variant_id, PostPastryMaterialSubVariantIngredients data, DatabaseContext context)
        {
            string response = "";

            string lastPastryMaterialSubVariantIngredientId = await IdFormat.GetNewestPastryMaterialSubVariantIngredientId(context);
            await TrackPastyMaterialSubVariantIngredientForInsertion(lastPastryMaterialSubVariantIngredientId, pastry_material_sub_variant_id, data, context);
            response = lastPastryMaterialSubVariantIngredientId;

            return response;
        }
        public static async Task<string> AddPastryMaterialSubVariantAddOn(string pastry_material_sub_variant_id, PostPastryMaterialSubVariantAddOns data, DatabaseContext context)
        {
            string response = "";

            string lastPastryMaterialSubVariantAddOnId = await IdFormat.GetNewestPastryMaterialSubVariantAddOnId(context);
            await TrackPastyMaterialSubVariantAddOnForInsertion(lastPastryMaterialSubVariantAddOnId, pastry_material_sub_variant_id, data, context);
            response = lastPastryMaterialSubVariantAddOnId;

            return response;
        }

        public static async Task<List<string>> AddPastryMaterialIngredient(string pastry_material_id, List<PostIngredients> data, DatabaseContext context)
        {
            List<string> response = new List<string>();
            if (data.IsNullOrEmpty()) { return response; }

            string lastPastryMaterialIngredientId = await IdFormat.GetNewestIngredientId(context);
            response.Add(lastPastryMaterialIngredientId);
            foreach (PostIngredients ingredient in data)
            {
                string newId = await TrackPastryMaterialIngredientForInsertion(lastPastryMaterialIngredientId, pastry_material_id, ingredient, context);
                lastPastryMaterialIngredientId = newId;

                response.Add(newId);
            }

            return response;
        }
        public static async Task<List<Guid>> AddPastryMaterialIngredientImportance(string pastry_material_id, List<PostPastryMaterialIngredientImportance> data, DatabaseContext context)
        {
            List<Guid> response = new List<Guid>();
            if (data.IsNullOrEmpty()) { return response; }

            foreach (PostPastryMaterialIngredientImportance ingredientImportance in data)
            {
                response.Add(await TrackPastryMaterialIngredientImportanceForInsertion(pastry_material_id, ingredientImportance, context));
            }

            return response;
        }
        public static async Task<List<string>> AddPastryMaterialAddOns(string pastry_material_id, List<PostPastryMaterialAddOns> data, DatabaseContext context)
        {
            List<string> response = new List<string>();
            if (data.IsNullOrEmpty()) { return response; }

            string lastPastryMaterialAddOnId = await IdFormat.GetNewestPastryMaterialAddOnId(context);
            response.Add(lastPastryMaterialAddOnId);
            foreach (PostPastryMaterialAddOns addOn in data)
            {
                string newId = await TrackPastryMaterialAddOnForInsertion(lastPastryMaterialAddOnId, pastry_material_id, addOn, context);
                lastPastryMaterialAddOnId = newId;

                response.Add(newId);
            }

            return response;
        }
        public static async Task<List<string>> AddPastryMaterialSubVariantIngredient(string pastry_material_sub_variant_id, List<PostPastryMaterialSubVariantIngredients> data, DatabaseContext context)
        {
            List<string> response = new List<string>();
            if (data.IsNullOrEmpty()) { return response; }

            string lastPastryMaterialSubVariantIngredientId = await IdFormat.GetNewestPastryMaterialSubVariantIngredientId(context);
            response.Add(lastPastryMaterialSubVariantIngredientId);
            foreach (PostPastryMaterialSubVariantIngredients subVariantIngredient in data)
            {
                string newId = await TrackPastyMaterialSubVariantIngredientForInsertion(lastPastryMaterialSubVariantIngredientId, pastry_material_sub_variant_id, subVariantIngredient, context);
                lastPastryMaterialSubVariantIngredientId = newId;

                response.Add(newId);
            }

            return response;
        }
        public static async Task<List<string>> AddPastryMaterialSubVariantAddOn(string pastry_material_sub_variant_id, List<PostPastryMaterialSubVariantAddOns> data, DatabaseContext context)
        {
            List<string> response = new List<string>();
            if (data.IsNullOrEmpty()) { return response; }

            string lastPastryMaterialSubVariantAddOnId = await IdFormat.GetNewestPastryMaterialSubVariantAddOnId(context);
            response.Add(lastPastryMaterialSubVariantAddOnId);
            foreach (PostPastryMaterialSubVariantAddOns subVariantAddOn in data)
            {
                string newId = await TrackPastyMaterialSubVariantAddOnForInsertion(lastPastryMaterialSubVariantAddOnId, pastry_material_sub_variant_id, subVariantAddOn, context);
                lastPastryMaterialSubVariantAddOnId = newId;

                response.Add(newId);
            }

            return response;
        }

        private static async Task<string> TrackPastryMaterialIngredientForInsertion(string ingredient_id, string pastry_material_id, PostIngredients data, DatabaseContext context)
        {
            string response = "";

            Ingredients newIngredientsEntry = new Ingredients();
            DateTime currentTime = DateTime.Now;

            newIngredientsEntry.ingredient_id = ingredient_id;

            newIngredientsEntry.pastry_material_id = pastry_material_id;

            newIngredientsEntry.item_id = data.item_id;
            newIngredientsEntry.ingredient_type = data.ingredient_type;

            newIngredientsEntry.amount = data.amount;
            newIngredientsEntry.amount_measurement = data.amount_measurement;
            newIngredientsEntry.is_active = true;
            newIngredientsEntry.date_added = currentTime;
            newIngredientsEntry.last_modified_date = currentTime;

            await context.Ingredients.AddAsync(newIngredientsEntry);

            response = IdFormat.IncrementId(IdPrefix.Ingredient, IdFormat.IdNumbersLength, ingredient_id);
            return response;
        }
        private static async Task<Guid> TrackPastryMaterialIngredientImportanceForInsertion(string pastry_material_id, PostPastryMaterialIngredientImportance data, DatabaseContext context)
        {
            Guid response;

            response = Guid.NewGuid();

            PastryMaterialIngredientImportance newIngredientImportanceEntry = new PastryMaterialIngredientImportance();
            DateTime currentTime = DateTime.Now;

            newIngredientImportanceEntry.pastry_material_ingredient_importance_id = response;
            newIngredientImportanceEntry.pastry_material_id = pastry_material_id;

            newIngredientImportanceEntry.item_id = data.item_id;
            newIngredientImportanceEntry.importance = data.importance;
            newIngredientImportanceEntry.ingredient_type = data.ingredient_type;

            newIngredientImportanceEntry.date_added = currentTime;
            newIngredientImportanceEntry.last_modified_date = currentTime;
            newIngredientImportanceEntry.is_active = true;

            await context.PastryMaterialIngredientImportance.AddAsync(newIngredientImportanceEntry);

            return response;
        }
        private static async Task<string> TrackPastryMaterialAddOnForInsertion(string pastry_material_add_on_id, string pastry_material_id, PostPastryMaterialAddOns data, DatabaseContext context)
        {
            string response = "";

            PastryMaterialAddOns newAddOnEntry = new PastryMaterialAddOns();
            DateTime currentTime = DateTime.Now;

            newAddOnEntry.pastry_material_add_on_id = pastry_material_add_on_id;

            newAddOnEntry.pastry_material_id = pastry_material_id;
            newAddOnEntry.add_ons_id = data.add_ons_id;
            newAddOnEntry.amount = data.amount;

            newAddOnEntry.is_active = true;
            newAddOnEntry.date_added = currentTime;
            newAddOnEntry.last_modified_date = currentTime;

            await context.PastryMaterialAddOns.AddAsync(newAddOnEntry);

            response = IdFormat.IncrementId(IdPrefix.PastryMaterialAddOn, IdFormat.IdNumbersLength, pastry_material_add_on_id);
            return response;
        }
        private static async Task<string> TrackPastyMaterialSubVariantIngredientForInsertion(string pastry_material_sub_variant_ingredient_id, string pastry_material_sub_variant_id, PostPastryMaterialSubVariantIngredients data, DatabaseContext context)
        {
            string response = "";

            PastryMaterialSubVariantIngredients newSubVariantIngredient = new PastryMaterialSubVariantIngredients();
            DateTime currentTime = DateTime.Now;

            newSubVariantIngredient.pastry_material_sub_variant_ingredient_id = pastry_material_sub_variant_ingredient_id;

            newSubVariantIngredient.pastry_material_sub_variant_id = pastry_material_sub_variant_id;

            newSubVariantIngredient.item_id = data.item_id;
            newSubVariantIngredient.ingredient_type = data.ingredient_type;
            newSubVariantIngredient.amount = data.amount;
            newSubVariantIngredient.amount_measurement = data.amount_measurement;

            newSubVariantIngredient.date_added = currentTime;
            newSubVariantIngredient.last_modified_date = currentTime;
            newSubVariantIngredient.is_active = true;


            await context.PastryMaterialSubVariantIngredients.AddAsync(newSubVariantIngredient);

            response = IdFormat.IncrementId(IdPrefix.PastryMaterialSubVariantIngredient, IdFormat.IdNumbersLength, pastry_material_sub_variant_ingredient_id);
            return response;
        }
        private static async Task<string> TrackPastyMaterialSubVariantAddOnForInsertion(string pastry_material_sub_variant_add_on_id, string pastry_material_sub_variant_id, PostPastryMaterialSubVariantAddOns data, DatabaseContext context)
        {
            string response = "";

            PastryMaterialSubVariantAddOns newSubVariantAddOn = new PastryMaterialSubVariantAddOns();
            DateTime currentTime = DateTime.Now;

            newSubVariantAddOn.pastry_material_sub_variant_add_on_id = pastry_material_sub_variant_add_on_id;
            newSubVariantAddOn.pastry_material_sub_variant_id = pastry_material_sub_variant_id;

            newSubVariantAddOn.add_ons_id = data.add_ons_id;
            newSubVariantAddOn.amount = data.amount;

            newSubVariantAddOn.date_added = currentTime;
            newSubVariantAddOn.last_modified_date = currentTime;
            newSubVariantAddOn.is_active = true;

            await context.PastryMaterialSubVariantAddOns.AddAsync(newSubVariantAddOn);

            response = IdFormat.IncrementId(IdPrefix.PastryMaterialSubVariantAddOn, IdFormat.IdNumbersLength, pastry_material_sub_variant_add_on_id);
            return response;
        }
    }
    public class DataRetrieval
    {
        public static async Task<PastryMaterials> GetPastryMaterialAsync(string pastry_material_id, DatabaseContext context)
        {
            PastryMaterials? currentPastryMaterial;
            try
            {
                currentPastryMaterial = await context.PastryMaterials.Where(x
                => x.is_active == true && x.pastry_material_id == pastry_material_id).FirstAsync();

                return currentPastryMaterial;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material with the id " + pastry_material_id + " found in the database.");
        }
        public static async Task<Ingredients> GetPastryMaterialIngredientAsync(string ingredient_id, DatabaseContext context)
        {
            Ingredients? currentIngredient;
            try
            {
                currentIngredient = await context.Ingredients.Where(x
                => x.is_active == true && x.ingredient_id == ingredient_id).FirstAsync();

                return currentIngredient;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material ingredient with the id " + ingredient_id + " found in the database.");
        }
        public static async Task<Ingredients> GetPastryMaterialIngredientAsync(string pastry_material_id, string ingredient_id, DatabaseContext context)
        {
            Ingredients? currentIngredient;
            try
            {
                currentIngredient = await context.Ingredients.Where(x
                => x.is_active == true && x.ingredient_id == ingredient_id && x.pastry_material_id == pastry_material_id).FirstAsync();

                return currentIngredient;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material ingredient with the id " + ingredient_id + " for the pastry material with the id " + pastry_material_id + " found in the database.");
        }
        public static async Task<PastryMaterialIngredientImportance> GetPastryMaterialIngredientImportanceAsync(Guid pastry_material_ingredient_importance_id, DatabaseContext context)
        {
            PastryMaterialIngredientImportance? currentIngredientImportance;
            try
            {
                currentIngredientImportance = await context.PastryMaterialIngredientImportance.Where(x
                => x.is_active == true && x.pastry_material_ingredient_importance_id == pastry_material_ingredient_importance_id).FirstAsync();

                return currentIngredientImportance;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material ingredient importance with the id " + pastry_material_ingredient_importance_id + " found in the database.");
        }
        public static async Task<PastryMaterialIngredientImportance> GetPastryMaterialIngredientImportanceAsync(string pastry_material_id, Guid pastry_material_ingredient_importance_id, DatabaseContext context)
        {
            PastryMaterialIngredientImportance? currentIngredientImportance;
            try
            {
                currentIngredientImportance = await context.PastryMaterialIngredientImportance.Where(x
                => x.is_active == true && x.pastry_material_ingredient_importance_id == pastry_material_ingredient_importance_id && x.pastry_material_id == pastry_material_id).FirstAsync();

                return currentIngredientImportance;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material ingredient importance with the id " + pastry_material_ingredient_importance_id + " for the pastry material with the id " + pastry_material_id + " found in the database.");
        }
        public static async Task<PastryMaterialAddOns> GetPastryMaterialAddOnAsync(string pastry_material_add_on_id, DatabaseContext context)
        {
            PastryMaterialAddOns? currentPastryMaterialAddOn = null;
            try
            {
                currentPastryMaterialAddOn = await context.PastryMaterialAddOns.Where(x => x.is_active == true && x.pastry_material_add_on_id == pastry_material_add_on_id).FirstAsync();
                return currentPastryMaterialAddOn;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material add on with the id " + pastry_material_add_on_id + " found in the database.");
        }
        public static async Task<PastryMaterialAddOns> GetPastryMaterialAddOnAsync(string pastry_material_id, string pastry_material_add_on_id, DatabaseContext context)
        {
            PastryMaterialAddOns? currentPastryMaterialAddOn = null;
            try
            {
                currentPastryMaterialAddOn = await context.PastryMaterialAddOns.Where(x => x.is_active == true && x.pastry_material_add_on_id == pastry_material_add_on_id && x.pastry_material_id == pastry_material_id).FirstAsync();
                return currentPastryMaterialAddOn;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material add on with the id " + pastry_material_add_on_id + " for the pastry material with the id " + pastry_material_id + " found in the database.");
        }

        public static async Task<PastryMaterialSubVariants> GetPastryMaterialSubVariantAsync(string pastry_material_sub_variant_id, DatabaseContext context)
        {
            PastryMaterialSubVariants? currentPastryMaterialSubVariant;
            try
            {
                currentPastryMaterialSubVariant = await context.PastryMaterialSubVariants.Where(x
                => x.is_active == true && x.pastry_material_sub_variant_id == pastry_material_sub_variant_id).FirstAsync();

                return currentPastryMaterialSubVariant;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material sub variant with the id " + pastry_material_sub_variant_id + " found in the database.");
        }
        public static async Task<PastryMaterialSubVariants> GetPastryMaterialSubVariantAsync(string pastry_material_id, string pastry_material_sub_variant_id, DatabaseContext context)
        {
            PastryMaterialSubVariants? currentPastryMaterialSubVariant;
            try
            {
                currentPastryMaterialSubVariant = await context.PastryMaterialSubVariants.Where(x
                => x.is_active == true && x.pastry_material_sub_variant_id == pastry_material_sub_variant_id && x.pastry_material_id == pastry_material_id).FirstAsync();

                return currentPastryMaterialSubVariant;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material sub variant with the id " + pastry_material_sub_variant_id + " for the pastry material with the id " + pastry_material_id + " found in the database.");
        }
        public static async Task<PastryMaterialSubVariantIngredients> GetPastryMaterialSubVariantIngredientAsync(string pastry_material_sub_variant_ingredient_id, DatabaseContext context)
        {
            PastryMaterialSubVariantIngredients? currentPastryMaterialSubVariantIngredient;
            try
            {
                currentPastryMaterialSubVariantIngredient = await context.PastryMaterialSubVariantIngredients.Where(x
                => x.is_active == true && x.pastry_material_sub_variant_ingredient_id == pastry_material_sub_variant_ingredient_id).FirstAsync();

                return currentPastryMaterialSubVariantIngredient;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material sub variant ingredient with the id " + pastry_material_sub_variant_ingredient_id + " found in the database.");
        }
        public static async Task<PastryMaterialSubVariantIngredients> GetPastryMaterialSubVariantIngredientAsync(string pastry_material_id, string pastry_material_sub_variant_id, string pastry_material_sub_variant_ingredient_id, DatabaseContext context)
        {
            PastryMaterialSubVariants? currentPastryMaterialSubVariant;
            try
            {
                currentPastryMaterialSubVariant = await context.PastryMaterialSubVariants.Where(x
                => x.is_active == true && x.pastry_material_sub_variant_id == pastry_material_sub_variant_id && x.pastry_material_id == pastry_material_id).FirstAsync();
            }
            catch { throw new NotFoundInDatabaseException("No pastry material sub variant with the id " + pastry_material_sub_variant_id + " for the pastry material with the id " + pastry_material_id + " found in the database."); }

            PastryMaterialSubVariantIngredients? currentPastryMaterialSubVariantIngredient;
            try
            {
                currentPastryMaterialSubVariantIngredient = await context.PastryMaterialSubVariantIngredients.Where(x
                => x.is_active == true && x.pastry_material_sub_variant_ingredient_id == pastry_material_sub_variant_ingredient_id && x.pastry_material_sub_variant_id == currentPastryMaterialSubVariant.pastry_material_sub_variant_id).FirstAsync();

                return currentPastryMaterialSubVariantIngredient;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material sub variant ingredient with the id " + pastry_material_sub_variant_ingredient_id + " for the pastry material sub variant " + pastry_material_sub_variant_id + " of the pastry material with the id " + pastry_material_id + " found in the database.");
        }
        public static async Task<PastryMaterialSubVariantAddOns> GetPastryMaterialSubVariantAddOnAsync(string pastry_material_sub_variant_add_on_id, DatabaseContext context)
        {
            PastryMaterialSubVariantAddOns? currentPastryMaterialSubVariantAddOn = null;
            try
            {
                currentPastryMaterialSubVariantAddOn = await context.PastryMaterialSubVariantAddOns.Where(x => x.is_active == true && x.pastry_material_sub_variant_add_on_id == pastry_material_sub_variant_add_on_id).FirstAsync();
                return currentPastryMaterialSubVariantAddOn;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material sub variant add on with the id " + pastry_material_sub_variant_add_on_id + " found in the database.");
        }
        public static async Task<PastryMaterialSubVariantAddOns> GetPastryMaterialSubVariantAddOnAsync(string pastry_material_id, string pastry_material_sub_variant_id, string pastry_material_sub_variant_add_on_id, DatabaseContext context)
        {
            PastryMaterialSubVariants? currentPastryMaterialSubVariant;
            try
            {
                currentPastryMaterialSubVariant = await context.PastryMaterialSubVariants.Where(x
                => x.is_active == true && x.pastry_material_sub_variant_id == pastry_material_sub_variant_id && x.pastry_material_id == pastry_material_id).FirstAsync();
            }
            catch { throw new NotFoundInDatabaseException("No pastry material sub variant with the id " + pastry_material_sub_variant_id + " for the pastry material with the id " + pastry_material_id + " found in the database."); }

            PastryMaterialSubVariantAddOns? currentPastryMaterialSubVariantAddOn = null;
            try
            {
                currentPastryMaterialSubVariantAddOn = await context.PastryMaterialSubVariantAddOns.Where(x => x.is_active == true && x.pastry_material_sub_variant_add_on_id == pastry_material_sub_variant_add_on_id && x.pastry_material_sub_variant_id == currentPastryMaterialSubVariant.pastry_material_sub_variant_id).FirstAsync();
                return currentPastryMaterialSubVariantAddOn;
            }
            catch { }
            throw new NotFoundInDatabaseException("No pastry material sub variant add on with the id " + pastry_material_sub_variant_add_on_id + " for the pastry material sub variant " + pastry_material_sub_variant_id + " of the pastry material with the id " + pastry_material_id + " found in the database.");
        }

        public static async Task<Item> GetInventoryItemAsync(string id, KaizenTables kaizenTables)
        {
            Item? currentInventoryItem;

            try
            {
                currentInventoryItem = await kaizenTables.Item.Where(x => x.is_active == true && x.id == Convert.ToInt32(id)).FirstAsync();
                return currentInventoryItem;
            }
            catch (FormatException exF) { throw new FormatException("Invalid id format for " + id + ", must be a value that can be parsed as an integer."); }
            catch (InvalidOperationException exO) { throw new NotFoundInDatabaseException("The id " + id + " does not exist in the inventory"); }
        }
        public static async Task<AddOns> GetAddOnItemAsync(int add_ons_id, KaizenTables kaizenTables)
        {
            AddOns? selectedAddOn = null;
            try
            {
                selectedAddOn = await kaizenTables.AddOns.Where(x => x.add_ons_id == add_ons_id).FirstAsync();

                return selectedAddOn;
            }
            catch (Exception e) { throw new NotFoundInDatabaseException("Add on with the id " + Convert.ToString(add_ons_id) + " does not exist."); }

        }
    }
    public class DataParser
    {
        public static async Task<GetPastryMaterial> CreatePastryMaterialResponseFromDBRow(PastryMaterials data, DatabaseContext context, KaizenTables kaizenTables)
        {
            GetPastryMaterial response = new GetPastryMaterial();
            response.design_id = Convert.ToBase64String(data.design_id);
            try { Designs? selectedDesign = await context.Designs.Where(x => x.is_active == true && x.design_id.SequenceEqual(data.design_id)).Select(x => new Designs { display_name = x.display_name }).FirstAsync(); response.design_name = selectedDesign.display_name; }
            catch (Exception e) { response.design_name = "N/A"; }

            response.pastry_material_id = data.pastry_material_id;
            response.date_added = data.date_added;
            response.last_modified_date = data.last_modified_date;
            response.main_variant_name = data.main_variant_name;
            response.ingredients_in_stock = true;

            List<GetPastryMaterialIngredients> responsePastryMaterialList = new List<GetPastryMaterialIngredients>();
            List<GetPastryMaterialIngredientImportance> responsePastryMaterialImportanceList = new List<GetPastryMaterialIngredientImportance>();
            List<GetPastryMaterialAddOns> responsePastryMaterialAddOns = new List<GetPastryMaterialAddOns>();
            List<GetPastryMaterialSubVariant> responsePastryMaterialSubVariants = new List<GetPastryMaterialSubVariant>();
            double calculatedCost = 0.0;

            List<Ingredients> currentPastryMaterialIngredients = await context.Ingredients.Where(x => x.is_active == true && x.pastry_material_id == data.pastry_material_id).ToListAsync();
            List<PastryMaterialIngredientImportance> currentPastryMaterialIngredientImportance = await context.PastryMaterialIngredientImportance.Where(x => x.is_active == true && x.pastry_material_id == data.pastry_material_id).ToListAsync();
            List<PastryMaterialAddOns> currentPastryMaterialAddOns = await context.PastryMaterialAddOns.Where(x => x.is_active == true && x.pastry_material_id == data.pastry_material_id).ToListAsync();

            Dictionary<string, double> baseVariantIngredientAmountDict = new Dictionary<string, double>(); //Contains the ingredients for the base variant
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
                            Item? currentInventoryItemI = null;
                            try { currentInventoryItemI = await DataRetrieval.GetInventoryItemAsync(currentIngredient.item_id, kaizenTables); }
                            catch { continue; }

                            newSubIngredientListEntry.item_name = currentInventoryItemI.item_name;
                            newSubIngredientListEntry.item_id = Convert.ToString(currentInventoryItemI.id);

                            string currentIngredientStringId = Convert.ToString(currentInventoryItemI.id);

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

                            if (baseVariantIngredientAmountDict.ContainsKey(currentIngredientStringId))
                            {
                                double currentIngredientTotalConsumption = baseVariantIngredientAmountDict[currentIngredientStringId];
                                baseVariantIngredientAmountDict[currentIngredientStringId] = currentIngredientTotalConsumption += convertedAmountI;
                            }
                            else
                            {
                                baseVariantIngredientAmountDict.Add(currentIngredientStringId, convertedAmountI);
                            }

                            if (baseVariantIngredientAmountDict[currentIngredientStringId] > currentInventoryItemI.quantity)
                            {
                                response.ingredients_in_stock = false;
                            }
                            calculatedCost += calculatedAmountI;
                            break;
                        }
                    case IngredientType.Material:
                        {
                            Materials? currentReferencedMaterial = await context.Materials.Where(x => x.material_id == currentIngredient.item_id && x.is_active == true).FirstAsync();
                            if (currentReferencedMaterial == null) { continue; }

                            newSubIngredientListEntry.item_name = currentReferencedMaterial.material_name;
                            newSubIngredientListEntry.item_id = currentReferencedMaterial.material_id;

                            List<MaterialIngredients> currentMaterialReferencedIngredients = await context.MaterialIngredients.Where(x => x.material_id == currentIngredient.item_id).ToListAsync();

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
                                            try { currentSubMaterialReferencedInventoryItem = await DataRetrieval.GetInventoryItemAsync(materialIngredients.item_id, kaizenTables); }
                                            catch { continue; }

                                            if (currentSubMaterialReferencedInventoryItem == null) { continue; }
                                            else { newEntryMaterialIngredientsEntry.item_name = currentSubMaterialReferencedInventoryItem.item_name; }
                                            break;
                                        case IngredientType.Material:
                                            Materials? currentSubMaterialReferencedMaterial = await context.Materials.Where(x => x.material_id == materialIngredients.item_id && x.is_active == true).FirstAsync();
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
                                try { currentReferencedIngredientM = await DataRetrieval.GetInventoryItemAsync(subIng.item_id, kaizenTables); }
                                catch { continue; }

                                string currentIngredientStringId = Convert.ToString(currentReferencedIngredientM.id);
                                double currentRefItemPrice = currentReferencedIngredientM.price;
                                double convertedAmount = 0.0;
                                double ingredientCost = 0.0;//currentReferencedIngredientM.measurements == subIng.amount_measurement ?
                                                            //(currentRefItemPrice * currentIngredient.amount) * currentSubIngredientCostMultiplier : 
                                                            //(currentRefItemPrice * UnitConverter.ConvertByName(currentIngredient.amount, amountQuantityType, amountUnitMeasurement, currentReferencedIngredientM.measurements) * currentSubIngredientCostMultiplier);
                                if (currentReferencedIngredientM.measurements == subIng.amount_measurement)
                                { convertedAmount = subIng.amount; }
                                else
                                { convertedAmount = UnitConverter.ConvertByName(subIng.amount, amountQuantityType, amountUnitMeasurement, currentReferencedIngredientM.measurements); }

                                if (baseVariantIngredientAmountDict.ContainsKey(currentIngredientStringId))
                                {
                                    double currentIngredientTotalConsumption = baseVariantIngredientAmountDict[currentIngredientStringId];
                                    baseVariantIngredientAmountDict[currentIngredientStringId] = currentIngredientTotalConsumption += convertedAmount;
                                }
                                else
                                {
                                    baseVariantIngredientAmountDict.Add(currentIngredientStringId, convertedAmount);
                                }

                                if (baseVariantIngredientAmountDict[currentIngredientStringId] > currentReferencedIngredientM.quantity)
                                {
                                    response.ingredients_in_stock = false;
                                }

                                ingredientCost = (currentRefItemPrice * convertedAmount) * currentSubIngredientCostMultiplier;
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

                                Materials currentReferencedMaterialForSub = await context.Materials.Where(x => x.is_active == true && x.material_id == currentSubMaterial.item_id).FirstAsync();

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

                                List<MaterialIngredients> subMaterialIngredients = await context.MaterialIngredients.Where(x => x.is_active == true && x.material_id == currentReferencedMaterialForSub.material_id).ToListAsync();
                                foreach (MaterialIngredients subMaterialIngredientsRow in subMaterialIngredients)
                                {
                                    switch (subMaterialIngredientsRow.ingredient_type)
                                    {
                                        case IngredientType.InventoryItem:

                                            Item? refItemForSubMatIng = null;
                                            try { refItemForSubMatIng = await DataRetrieval.GetInventoryItemAsync(subMaterialIngredientsRow.item_id, kaizenTables); }
                                            catch (Exception e) { continue; }

                                            string currentIngredientStringId = Convert.ToString(refItemForSubMatIng.id);

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

                                            double convertedAmountSubMaterialIngredient = 0.0;
                                            double currentSubMaterialIngredientPrice = 0.0; //refItemForSubMatIng.measurements == subMaterialIngredientsRow.amount_measurement ? 
                                                                                            //(refItemPrice * subMatIngRowAmount) * costMultiplier : 
                                                                                            //(refItemPrice * UnitConverter.ConvertByName(subMatIngRowAmount, refItemQuantityUnit, subMatIngRowMeasurement, refItemMeasurement)) * costMultiplier;

                                            if (refItemForSubMatIng.measurements == subMaterialIngredientsRow.amount_measurement) { convertedAmountSubMaterialIngredient = subMatIngRowAmount; }
                                            else { convertedAmountSubMaterialIngredient = UnitConverter.ConvertByName(subMatIngRowAmount, refItemQuantityUnit, subMatIngRowMeasurement, refItemMeasurement); }

                                            if (baseVariantIngredientAmountDict.ContainsKey(currentIngredientStringId))
                                            {
                                                double currentIngredientTotalConsumption = baseVariantIngredientAmountDict[currentIngredientStringId];
                                                baseVariantIngredientAmountDict[currentIngredientStringId] = currentIngredientTotalConsumption += convertedAmountSubMaterialIngredient;
                                            }
                                            else
                                            {
                                                baseVariantIngredientAmountDict.Add(currentIngredientStringId, convertedAmountSubMaterialIngredient);
                                            }

                                            if (baseVariantIngredientAmountDict[currentIngredientStringId] > refItemForSubMatIng.quantity)
                                            {
                                                response.ingredients_in_stock = false;
                                            }

                                            currentSubMaterialIngredientPrice = (refItemPrice * subMatIngRowAmount) * costMultiplier;

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
            foreach (PastryMaterialIngredientImportance currentIngredientImportance in currentPastryMaterialIngredientImportance)
            {
                GetPastryMaterialIngredientImportance newResponseImportanceListEntry = new GetPastryMaterialIngredientImportance();
                newResponseImportanceListEntry.pastry_material_id = currentIngredientImportance.pastry_material_id;
                newResponseImportanceListEntry.pastry_material_ingredient_importance_id = currentIngredientImportance.pastry_material_ingredient_importance_id;

                newResponseImportanceListEntry.item_id = currentIngredientImportance.item_id;
                newResponseImportanceListEntry.ingredient_type = currentIngredientImportance.ingredient_type;
                newResponseImportanceListEntry.importance = currentIngredientImportance.importance;

                newResponseImportanceListEntry.date_added = currentIngredientImportance.date_added;
                newResponseImportanceListEntry.last_modified_date = currentIngredientImportance.last_modified_date;

                responsePastryMaterialImportanceList.Add(newResponseImportanceListEntry);
            }
            foreach (PastryMaterialAddOns currentAddOn in currentPastryMaterialAddOns)
            {
                AddOns? referencedAddOns = null;
                try { referencedAddOns = await kaizenTables.AddOns.Where(x => x.add_ons_id == currentAddOn.add_ons_id).FirstAsync(); }
                catch { continue; }
                if (referencedAddOns == null) { continue; }

                GetPastryMaterialAddOns newResponseAddOnRow = new GetPastryMaterialAddOns();
                newResponseAddOnRow.pastry_material_add_on_id = currentAddOn.pastry_material_add_on_id;
                newResponseAddOnRow.pastry_material_id = currentAddOn.pastry_material_id;

                newResponseAddOnRow.add_ons_id = currentAddOn.add_ons_id;
                newResponseAddOnRow.add_ons_name = referencedAddOns.name;
                newResponseAddOnRow.amount = currentAddOn.amount;

                newResponseAddOnRow.date_added = currentAddOn.date_added;
                newResponseAddOnRow.last_modified_date = currentAddOn.last_modified_date;
                responsePastryMaterialAddOns.Add(newResponseAddOnRow);
            }

            List<PastryMaterialSubVariants> currentPastryMaterialSubVariants = await context.PastryMaterialSubVariants.Where(x => x.is_active == true && x.pastry_material_id == data.pastry_material_id).ToListAsync();
            foreach (PastryMaterialSubVariants currentSubVariant in currentPastryMaterialSubVariants)
            {
                GetPastryMaterialSubVariant newSubVariantListRow = new GetPastryMaterialSubVariant();
                newSubVariantListRow.pastry_material_id = currentSubVariant.pastry_material_id;
                newSubVariantListRow.pastry_material_sub_variant_id = currentSubVariant.pastry_material_sub_variant_id;
                newSubVariantListRow.sub_variant_name = currentSubVariant.sub_variant_name;
                newSubVariantListRow.date_added = currentSubVariant.date_added;
                newSubVariantListRow.last_modified_date = currentSubVariant.last_modified_date;
                newSubVariantListRow.ingredients_in_stock = response.ingredients_in_stock == true ? true : false;
                double estimatedCostSubVariant = calculatedCost;

                List<PastryMaterialSubVariantIngredients> currentSubVariantIngredients = await context.PastryMaterialSubVariantIngredients.Where(x => x.is_active == true && x.pastry_material_sub_variant_id == currentSubVariant.pastry_material_sub_variant_id).ToListAsync();
                List<PastryMaterialSubVariantAddOns> currentSubVariantAddOns = await context.PastryMaterialSubVariantAddOns.Where(x => x.is_active == true && x.pastry_material_sub_variant_id == currentSubVariant.pastry_material_sub_variant_id).ToListAsync();

                List<SubGetPastryMaterialSubVariantIngredients> currentSubVariantIngredientList = new List<SubGetPastryMaterialSubVariantIngredients>();
                List<GetPastryMaterialSubVariantAddOns> currentSubVariantAddOnList = new List<GetPastryMaterialSubVariantAddOns>();

                string baseVariantJson = JsonSerializer.Serialize(baseVariantIngredientAmountDict);
                Dictionary<string, double>? subVariantIngredientConsumptionDict = JsonSerializer.Deserialize<Dictionary<string, double>>(baseVariantJson);

                foreach (PastryMaterialSubVariantIngredients currentSubVariantIngredient in currentSubVariantIngredients)
                {
                    SubGetPastryMaterialSubVariantIngredients newSubVariantIngredientListEntry = new SubGetPastryMaterialSubVariantIngredients();
                    newSubVariantIngredientListEntry.pastry_material_sub_variant_id = currentSubVariantIngredient.pastry_material_sub_variant_id;
                    newSubVariantIngredientListEntry.pastry_material_sub_variant_ingredient_id = currentSubVariantIngredient.pastry_material_sub_variant_ingredient_id;

                    newSubVariantIngredientListEntry.date_added = currentSubVariantIngredient.date_added;
                    newSubVariantIngredientListEntry.last_modified_date = currentSubVariantIngredient.last_modified_date;

                    newSubVariantIngredientListEntry.ingredient_type = currentSubVariantIngredient.ingredient_type;
                    newSubVariantIngredientListEntry.amount_measurement = currentSubVariantIngredient.amount_measurement;
                    newSubVariantIngredientListEntry.amount = currentSubVariantIngredient.amount;
                    //Check if the measurement unit in the ingredient record is valid
                    //If not found, skip current ingredient
                    string? amountQuantityType = null;
                    string? amountUnitMeasurement = null;

                    bool isAmountMeasurementValid = false;
                    foreach (string unitQuantity in validMeasurementUnits.Keys)
                    {
                        List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                        string? currentMeasurement = currentQuantityUnits.Find(x => x.Equals(currentSubVariantIngredient.amount_measurement));

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
                        throw new InvalidOperationException("The sub pastry material ingredient of " + currentSubVariant.pastry_material_sub_variant_id + " has an ingredient with an invalid measurement unit: " + currentSubVariantIngredient.pastry_material_sub_variant_ingredient_id);
                    }

                    switch (currentSubVariantIngredient.ingredient_type)
                    {
                        case IngredientType.InventoryItem:
                            {
                                Item? currentInventoryItemI = null;
                                try { currentInventoryItemI = await DataRetrieval.GetInventoryItemAsync(currentSubVariantIngredient.item_id, kaizenTables); }
                                catch { continue; }

                                newSubVariantIngredientListEntry.item_name = currentInventoryItemI.item_name;
                                newSubVariantIngredientListEntry.item_id = Convert.ToString(currentInventoryItemI.id);
                                string currentIngredientStringId = Convert.ToString(currentInventoryItemI.id);
                                double convertedAmountI = 0.0;
                                double calculatedAmountI = 0.0;
                                if (amountQuantityType != "Count")
                                {
                                    convertedAmountI = UnitConverter.ConvertByName(currentSubVariantIngredient.amount, amountQuantityType, amountUnitMeasurement, currentInventoryItemI.measurements);
                                    calculatedAmountI = convertedAmountI * currentInventoryItemI.price;
                                }
                                else
                                {
                                    convertedAmountI = currentSubVariantIngredient.amount;
                                    calculatedAmountI = convertedAmountI * currentInventoryItemI.price;
                                }

                                if (subVariantIngredientConsumptionDict.ContainsKey(currentIngredientStringId))
                                {
                                    double currentIngredientTotalConsumption = subVariantIngredientConsumptionDict[currentIngredientStringId];
                                    subVariantIngredientConsumptionDict[currentIngredientStringId] = currentIngredientTotalConsumption += convertedAmountI;
                                }
                                else
                                {
                                    subVariantIngredientConsumptionDict.Add(currentIngredientStringId, convertedAmountI);
                                }

                                if (subVariantIngredientConsumptionDict[currentIngredientStringId] > currentInventoryItemI.quantity)
                                {
                                    newSubVariantListRow.ingredients_in_stock = false;
                                }

                                estimatedCostSubVariant += calculatedAmountI;
                                break;
                            }
                        case IngredientType.Material:
                            {
                                Materials? currentReferencedMaterial = await context.Materials.Where(x => x.material_id == currentSubVariantIngredient.item_id && x.is_active == true).FirstAsync();
                                if (currentReferencedMaterial == null) { continue; }

                                newSubVariantIngredientListEntry.item_name = currentReferencedMaterial.material_name;
                                newSubVariantIngredientListEntry.item_id = currentReferencedMaterial.material_id;

                                List<MaterialIngredients> currentMaterialReferencedIngredients = await context.MaterialIngredients.Where(x => x.material_id == currentSubVariantIngredient.item_id).ToListAsync();

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
                                                try { currentSubMaterialReferencedInventoryItem = await DataRetrieval.GetInventoryItemAsync(materialIngredients.item_id, kaizenTables); }
                                                catch { continue; }
                                                newEntryMaterialIngredientsEntry.item_name = currentSubMaterialReferencedInventoryItem.item_name;
                                                break;
                                            case IngredientType.Material:
                                                Materials? currentSubMaterialReferencedMaterial = await context.Materials.Where(x => x.material_id == materialIngredients.item_id && x.is_active == true).FirstAsync();
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
                                    newSubVariantIngredientListEntry.material_ingredients = newEntryMaterialIngredients;
                                }
                                else
                                {
                                    newSubVariantIngredientListEntry.material_ingredients = new List<SubGetMaterialIngredients>();
                                }
                                //Price calculation code
                                //Get all ingredient for currently referenced material
                                List<MaterialIngredients> subIngredientsForcurrentSubVariantIngredient = currentMaterialReferencedIngredients.Where(x => x.ingredient_type == IngredientType.InventoryItem).ToList();
                                double currentSubIngredientCostMultiplier = amountUnitMeasurement.Equals(currentReferencedMaterial.amount_measurement) ? currentReferencedMaterial.amount / currentSubVariantIngredient.amount : currentReferencedMaterial.amount / UnitConverter.ConvertByName(currentSubVariantIngredient.amount, amountQuantityType, amountUnitMeasurement, currentReferencedMaterial.amount_measurement);
                                foreach (MaterialIngredients subIng in subIngredientsForcurrentSubVariantIngredient)
                                {
                                    Item? currentReferencedIngredientM = null;
                                    try { currentReferencedIngredientM = await DataRetrieval.GetInventoryItemAsync(subIng.item_id, kaizenTables); }
                                    catch (Exception e) { continue; }

                                    string currentIngredientStringId = Convert.ToString(currentReferencedIngredientM.id);
                                    double currentRefItemPrice = currentReferencedIngredientM.price;
                                    double convertedAmount = 0.0;
                                    double ingredientCost = 0.0;//currentReferencedIngredientM.measurements == subIng.amount_measurement ? (currentRefItemPrice * currentSubVariantIngredient.amount) * currentSubIngredientCostMultiplier : (currentRefItemPrice * UnitConverter.ConvertByName(currentSubVariantIngredient.amount, amountQuantityType, amountUnitMeasurement, currentReferencedIngredientM.measurements) * currentSubIngredientCostMultiplier);

                                    if (currentReferencedIngredientM.measurements == subIng.amount_measurement)
                                    { convertedAmount = subIng.amount; }
                                    else
                                    { convertedAmount = UnitConverter.ConvertByName(subIng.amount, amountQuantityType, amountUnitMeasurement, currentReferencedIngredientM.measurements); }

                                    if (subVariantIngredientConsumptionDict.ContainsKey(currentIngredientStringId))
                                    {
                                        double currentIngredientTotalConsumption = subVariantIngredientConsumptionDict[currentIngredientStringId];
                                        subVariantIngredientConsumptionDict[currentIngredientStringId] = currentIngredientTotalConsumption += convertedAmount;
                                    }
                                    else
                                    {
                                        subVariantIngredientConsumptionDict.Add(currentIngredientStringId, convertedAmount);
                                    }

                                    if (subVariantIngredientConsumptionDict[currentIngredientStringId] > currentReferencedIngredientM.quantity)
                                    {
                                        newSubVariantListRow.ingredients_in_stock = false;
                                    }

                                    ingredientCost = (currentRefItemPrice * convertedAmount) * currentSubIngredientCostMultiplier;

                                    estimatedCostSubVariant += ingredientCost;
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

                                    Materials currentReferencedMaterialForSub = await context.Materials.Where(x => x.is_active == true && x.material_id == currentSubMaterial.item_id).FirstAsync();

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

                                    List<MaterialIngredients> subMaterialIngredients = await context.MaterialIngredients.Where(x => x.is_active == true && x.material_id == currentReferencedMaterialForSub.material_id).ToListAsync();
                                    foreach (MaterialIngredients subMaterialIngredientsRow in subMaterialIngredients)
                                    {
                                        switch (subMaterialIngredientsRow.ingredient_type)
                                        {
                                            case IngredientType.InventoryItem:
                                                Item? refItemForSubMatIng = null;
                                                try { refItemForSubMatIng = await DataRetrieval.GetInventoryItemAsync(subMaterialIngredientsRow.item_id, kaizenTables); }
                                                catch (Exception e) { continue; }

                                                string currentIngredientStringId = Convert.ToString(refItemForSubMatIng.id);

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

                                                //double currentSubMaterialIngredientPrice = //refItemForSubMatIng.measurements == subMaterialIngredientsRow.amount_measurement ? (refItemPrice * subMatIngRowAmount) * costMultiplier : (refItemPrice * UnitConverter.ConvertByName(subMatIngRowAmount, refItemQuantityUnit, subMatIngRowMeasurement, refItemMeasurement)) * costMultiplier;
                                                double convertedAmountSubMaterialIngredient = 0.0;
                                                double currentSubMaterialIngredientPrice = 0.0; //refItemForSubMatIng.measurements == subMaterialIngredientsRow.amount_measurement ? 
                                                                                                //(refItemPrice * subMatIngRowAmount) * costMultiplier : 
                                                                                                //(refItemPrice * UnitConverter.ConvertByName(subMatIngRowAmount, refItemQuantityUnit, subMatIngRowMeasurement, refItemMeasurement)) * costMultiplier;

                                                if (refItemForSubMatIng.measurements == subMaterialIngredientsRow.amount_measurement) { convertedAmountSubMaterialIngredient = subMatIngRowAmount; }
                                                else { convertedAmountSubMaterialIngredient = UnitConverter.ConvertByName(subMatIngRowAmount, refItemQuantityUnit, subMatIngRowMeasurement, refItemMeasurement); }

                                                if (subVariantIngredientConsumptionDict.ContainsKey(currentIngredientStringId))
                                                {
                                                    double currentIngredientTotalConsumption = subVariantIngredientConsumptionDict[currentIngredientStringId];
                                                    subVariantIngredientConsumptionDict[currentIngredientStringId] = currentIngredientTotalConsumption += convertedAmountSubMaterialIngredient;
                                                }
                                                else
                                                {
                                                    subVariantIngredientConsumptionDict.Add(currentIngredientStringId, convertedAmountSubMaterialIngredient);
                                                }

                                                if (subVariantIngredientConsumptionDict[currentIngredientStringId] > refItemForSubMatIng.quantity)
                                                {
                                                    newSubVariantListRow.ingredients_in_stock = false;
                                                }

                                                currentSubMaterialIngredientPrice = (refItemPrice * subMatIngRowAmount) * costMultiplier;

                                                estimatedCostSubVariant += currentSubMaterialIngredientPrice;
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
                    currentSubVariantIngredientList.Add(newSubVariantIngredientListEntry);
                }
                foreach (PastryMaterialSubVariantAddOns currentSubVariantAddOn in currentSubVariantAddOns)
                {
                    Schemas.AddOns? referencedAddOns = null;
                    try { referencedAddOns = await kaizenTables.AddOns.Where(x => x.add_ons_id == currentSubVariantAddOn.add_ons_id).FirstAsync(); }
                    catch { continue; }
                    if (referencedAddOns == null) { continue; }


                    GetPastryMaterialSubVariantAddOns newResponseSubVariantAddOnRow = new GetPastryMaterialSubVariantAddOns();
                    newResponseSubVariantAddOnRow.pastry_material_sub_variant_add_on_id = currentSubVariantAddOn.pastry_material_sub_variant_add_on_id;
                    newResponseSubVariantAddOnRow.pastry_material_sub_variant_id = currentSubVariantAddOn.pastry_material_sub_variant_id;

                    newResponseSubVariantAddOnRow.add_ons_id = currentSubVariantAddOn.add_ons_id;
                    newResponseSubVariantAddOnRow.add_ons_name = referencedAddOns.name;
                    newResponseSubVariantAddOnRow.amount = currentSubVariantAddOn.amount;

                    newResponseSubVariantAddOnRow.date_added = currentSubVariantAddOn.date_added;
                    newResponseSubVariantAddOnRow.last_modified_date = currentSubVariantAddOn.last_modified_date;
                    currentSubVariantAddOnList.Add(newResponseSubVariantAddOnRow);
                }

                newSubVariantListRow.cost_estimate = estimatedCostSubVariant;
                newSubVariantListRow.sub_variant_ingredients = currentSubVariantIngredientList;
                newSubVariantListRow.sub_variant_add_ons = currentSubVariantAddOnList;

                responsePastryMaterialSubVariants.Add(newSubVariantListRow);
            }

            response.ingredients = responsePastryMaterialList;
            response.ingredient_importance = responsePastryMaterialImportanceList;
            response.add_ons = responsePastryMaterialAddOns;
            response.sub_variants = responsePastryMaterialSubVariants;
            response.cost_estimate = calculatedCost;

            return response;
        }
        public static async Task<GetDesign> CreateGetDesignResponseFromDbRow(Designs data, DatabaseContext context, KaizenTables kaizenTables)
        {
            GetDesign response = new GetDesign();

            response.design_id = data.design_id;
            response.display_name = data.display_name;
            response.design_picture_url = data.display_picture_url;
            response.cake_description = data.cake_description;
            response.design_tags = new List<GetDesignTag>();
            response.design_shapes = new List<GetDesignShape>();

            List<DesignTagsForCakes> cakeTags = await context.DesignTagsForCakes.Include(x => x.DesignTags).Where(x => x.is_active == true && x.design_id == data.design_id && x.DesignTags.is_active == true).ToListAsync();
            List<DesignShapes> cakeShapes = await context.DesignShapes.Where(x => x.is_active == true && x.design_id == data.design_id).ToListAsync();

            DesignImage? image;
            try { image = await context.DesignImage.Where(x => x.is_active == true && x.design_id == data.design_id).FirstAsync(); }
            catch { image = null; }

            foreach (DesignTagsForCakes currentTag in cakeTags)
            {
                if (currentTag.DesignTags != null)
                {
                    response.design_tags.Add(new GetDesignTag { design_tag_id = currentTag.DesignTags.design_tag_id, design_tag_name = currentTag.DesignTags.design_tag_name });
                }
            }
            foreach (DesignShapes currentShape in cakeShapes)
            {
                response.design_shapes.Add(new GetDesignShape
                {
                    design_shape_id = currentShape.design_shape_id,
                    shape_name = currentShape.shape_name
                });
            }

            if (image != null) { response.display_picture_data = image.picture_data; }
            else { response.display_picture_data = null; };
            return response;
        }

        public static async Task<Dictionary<string, InventorySubtractorInfo>> GetTotalIngredientAmountList(string variant_id, DatabaseContext context, KaizenTables kaizenTables)
        {
            Dictionary<string, InventorySubtractorInfo> response = new Dictionary<string, InventorySubtractorInfo>();

            PastryMaterials? selectedPastryMaterial = null;
            PastryMaterialSubVariants? selectedPastryMaterialSubVariant = null;

            try { selectedPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(variant_id, context); }
            catch (NotFoundInDatabaseException e) { }
            try { selectedPastryMaterialSubVariant = await DataRetrieval.GetPastryMaterialSubVariantAsync(variant_id, context); }
            catch (NotFoundInDatabaseException e) { }

            if (selectedPastryMaterial == null && selectedPastryMaterialSubVariant == null) { throw new NotFoundInDatabaseException(variant_id + " does not exist in both pastry material and the subvariant tables"); }
            if (selectedPastryMaterial == null && selectedPastryMaterialSubVariant != null) { selectedPastryMaterial = await DataRetrieval.GetPastryMaterialAsync(selectedPastryMaterialSubVariant.pastry_material_id, context); }
            if (selectedPastryMaterial == null) { throw new NotFoundInDatabaseException(variant_id + " exist in subvariant table, but the base pastry material it points to does not exist"); }

            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients

            List<Ingredients> baseIngredients = await context.Ingredients.Where(x => x.is_active == true && x.pastry_material_id == selectedPastryMaterial.pastry_material_id).ToListAsync();
            foreach (Ingredients currentBaseIngredient in baseIngredients)
            {
                //Check if the measurement unit in the ingredient record is valid
                //If not found, skip current ingredient
                string? amountQuantityType = null;
                string? amountUnitMeasurement = null;

                bool isAmountMeasurementValid = false;

                foreach (string unitQuantity in validMeasurementUnits.Keys)
                {
                    List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                    string? currentMeasurement = currentQuantityUnits.Find(x => x.Equals(currentBaseIngredient.amount_measurement));

                    if (currentMeasurement == null) { continue; }
                    else
                    {
                        isAmountMeasurementValid = true;
                        amountQuantityType = unitQuantity;
                        amountUnitMeasurement = currentMeasurement;
                    }

                }
                if (isAmountMeasurementValid == false) { throw new InvalidAmountMeasurementException("The measurement of the pastry ingredient with the id " + currentBaseIngredient.ingredient_id + " is not valid."); }

                switch (currentBaseIngredient.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        {
                            //Find the referred item
                            Item? currentRefInvItem = null;
                            try
                            { currentRefInvItem = await DataRetrieval.GetInventoryItemAsync(currentBaseIngredient.item_id, kaizenTables); }
                            catch (FormatException e) { throw new FormatException("The pastry ingredient with the type of " + IngredientType.InventoryItem + " and the ingredient id " + currentBaseIngredient.ingredient_id + " cannot be parsed as an integer"); }
                            catch (NotFoundInDatabaseException e) { throw new NotFoundInDatabaseException("The pastry ingredient with the type of " + IngredientType.InventoryItem + " and the item id " + currentBaseIngredient.item_id + " does not exist in the inventory"); }

                            string currentItemMeasurement = currentBaseIngredient.amount_measurement;
                            double currentItemAmount = currentBaseIngredient.amount;

                            //Calculate the value to subtract here
                            InventorySubtractorInfo? inventorySubtractorInfoForCurrentBaseIngredient = null;
                            response.TryGetValue(currentBaseIngredient.item_id, out inventorySubtractorInfoForCurrentBaseIngredient);

                            if (inventorySubtractorInfoForCurrentBaseIngredient == null)
                            {
                                response.Add(currentBaseIngredient.item_id, new InventorySubtractorInfo(amountQuantityType, amountUnitMeasurement, currentBaseIngredient.amount));
                            }
                            else
                            {
                                if (inventorySubtractorInfoForCurrentBaseIngredient.AmountUnit == currentItemMeasurement) { inventorySubtractorInfoForCurrentBaseIngredient.Amount += currentItemAmount; }
                                else
                                {
                                    double amountInRecordedUnit = UnitConverter.ConvertByName(currentItemAmount, inventorySubtractorInfoForCurrentBaseIngredient.AmountQuantityType, currentItemMeasurement, inventorySubtractorInfoForCurrentBaseIngredient.AmountUnit);
                                    inventorySubtractorInfoForCurrentBaseIngredient.Amount += amountInRecordedUnit;
                                }
                            }
                            break;
                        }
                    case IngredientType.Material:
                        {
                            List<MaterialIngredients> currentMaterialIngredients = await context.MaterialIngredients.Where(x => x.is_active == true && x.material_id == currentBaseIngredient.item_id).ToListAsync();

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
                                    List<MaterialIngredients> newEntriesToLoopThru = await context.MaterialIngredients.Where(x => x.is_active == true && x.material_ingredient_id == currentMatIngInLoop.material_ingredient_id).ToListAsync();
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
                                { currentRefInvItem = await DataRetrieval.GetInventoryItemAsync(currentMaterialIngredient.item_id, kaizenTables); }
                                catch (FormatException e) { throw new FormatException("The material ingredient for " + currentMaterialIngredient.material_id + " with the id " + currentMaterialIngredient.material_ingredient_id + ", failed to parse its item id " + currentMaterialIngredient.item_id + " as an integer"); }
                                catch (NotFoundInDatabaseException e) { throw new NotFoundInDatabaseException("The material ingredient for " + currentMaterialIngredient.material_id + " with the id " + currentMaterialIngredient.material_ingredient_id + ",  its item id " + currentMaterialIngredient.item_id + " does not refer to any active inventory record"); }

                                string currentItemMeasurement = currentMaterialIngredient.amount_measurement;
                                double currentItemAmount = currentMaterialIngredient.amount;

                                //Calculate the value to subtract here
                                InventorySubtractorInfo? inventorySubtractorInfoForCurrentBaseIngredient = null;
                                response.TryGetValue(currentBaseIngredient.item_id, out inventorySubtractorInfoForCurrentBaseIngredient);
                                if (inventorySubtractorInfoForCurrentBaseIngredient == null)
                                {
                                    response.Add(currentBaseIngredient.item_id, new InventorySubtractorInfo(amountQuantityType, amountUnitMeasurement, currentBaseIngredient.amount));
                                }
                                else
                                {
                                    if (inventorySubtractorInfoForCurrentBaseIngredient.AmountUnit == currentItemMeasurement) { inventorySubtractorInfoForCurrentBaseIngredient.Amount += currentItemAmount; }
                                    else
                                    {
                                        double amountInRecordedUnit = UnitConverter.ConvertByName(currentItemAmount, inventorySubtractorInfoForCurrentBaseIngredient.AmountQuantityType, currentItemMeasurement, inventorySubtractorInfoForCurrentBaseIngredient.AmountUnit);
                                        inventorySubtractorInfoForCurrentBaseIngredient.Amount += amountInRecordedUnit;
                                    }
                                }
                                break;
                            }
                            break;
                        }
                }
            }

            if (selectedPastryMaterialSubVariant != null)
            {
                List<PastryMaterialSubVariantIngredients> currentVariantIngredients = await context.PastryMaterialSubVariantIngredients.Where(x => x.is_active == true && x.pastry_material_sub_variant_id == selectedPastryMaterialSubVariant.pastry_material_sub_variant_id).ToListAsync();

                foreach (PastryMaterialSubVariantIngredients currentSubVariantIngredient in currentVariantIngredients)
                {
                    //Check if the measurement unit in the ingredient record is valid
                    //If not found, skip current ingredient
                    string? amountQuantityType = null;
                    string? amountUnitMeasurement = null;

                    bool isAmountMeasurementValid = false;
                    foreach (string unitQuantity in validMeasurementUnits.Keys)
                    {
                        List<string> currentQuantityUnits = validMeasurementUnits[unitQuantity];

                        string? currentMeasurement = currentQuantityUnits.Find(x => x.Equals(currentSubVariantIngredient.amount_measurement));

                        if (currentMeasurement == null) { continue; }
                        else
                        {
                            isAmountMeasurementValid = true;
                            amountQuantityType = unitQuantity;
                            amountUnitMeasurement = currentMeasurement;
                        }
                    }
                    if (isAmountMeasurementValid == false) { throw new InvalidAmountMeasurementException("The measurement of the pastry material sub variant ingredient with the id " + currentSubVariantIngredient.pastry_material_sub_variant_ingredient_id + " is not valid."); }

                    switch (currentSubVariantIngredient.ingredient_type)
                    {
                        case IngredientType.InventoryItem:
                            {
                                //Find the referred item
                                Item? currentRefInvItem = null;
                                try
                                { currentRefInvItem = await DataRetrieval.GetInventoryItemAsync(currentSubVariantIngredient.item_id, kaizenTables); }
                                catch (FormatException e) { throw new FormatException("The pastry sub variant ingredient with the type of " + IngredientType.InventoryItem + " and the ingredient id " + currentSubVariantIngredient.pastry_material_sub_variant_ingredient_id + " cannot be parsed as an integer"); }
                                catch (NotFoundInDatabaseException e) { throw new NotFoundInDatabaseException("The pastry sub variant ingredient with the type of " + IngredientType.InventoryItem + " and the item id " + currentSubVariantIngredient.item_id + " does not exist in the inventory"); }

                                string currentItemMeasurement = currentSubVariantIngredient.amount_measurement;
                                double currentItemAmount = currentSubVariantIngredient.amount;

                                //Calculate the value to subtract here
                                InventorySubtractorInfo? inventorySubtractorInfoForCurrentIngredient = null;
                                response.TryGetValue(currentSubVariantIngredient.item_id, out inventorySubtractorInfoForCurrentIngredient);

                                if (inventorySubtractorInfoForCurrentIngredient == null)
                                {
                                    response.Add(currentSubVariantIngredient.item_id, new InventorySubtractorInfo(amountQuantityType, amountUnitMeasurement, currentSubVariantIngredient.amount));
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
                                List<MaterialIngredients> currentMaterialIngredients = await context.MaterialIngredients.Where(x => x.is_active == true && x.material_id == currentSubVariantIngredient.item_id).ToListAsync();

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
                                        List<MaterialIngredients> newEntriesToLoopThru = await context.MaterialIngredients.Where(x => x.is_active == true && x.material_ingredient_id == currentMatIngInLoop.material_ingredient_id).ToListAsync();
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
                                    { currentRefInvItem = await DataRetrieval.GetInventoryItemAsync(currentMaterialIngredient.item_id, kaizenTables); }
                                    catch (FormatException e) { throw new FormatException("The material ingredient for " + currentMaterialIngredient.material_id + " with the id " + currentMaterialIngredient.material_ingredient_id + ", failed to parse its item id " + currentMaterialIngredient.item_id + " as an integer"); }
                                    catch (NotFoundInDatabaseException e) { throw new NotFoundInDatabaseException("The material ingredient for " + currentMaterialIngredient.material_id + " with the id " + currentMaterialIngredient.material_ingredient_id + ",  its item id " + currentMaterialIngredient.item_id + " does not refer to any active inventory record"); }

                                    string currentItemMeasurement = currentMaterialIngredient.amount_measurement;
                                    double currentItemAmount = currentMaterialIngredient.amount;

                                    //Calculate the value to subtract here
                                    InventorySubtractorInfo? inventorySubtractorInfoForCurrentIngredient = null;
                                    response.TryGetValue(currentSubVariantIngredient.item_id, out inventorySubtractorInfoForCurrentIngredient);
                                    if (inventorySubtractorInfoForCurrentIngredient == null)
                                    {
                                        response.Add(currentSubVariantIngredient.item_id, new InventorySubtractorInfo(amountQuantityType, amountUnitMeasurement, currentSubVariantIngredient.amount));
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

            return response;
        }

    }
    public class PriceCalculator
    {
        public static async Task<double> CalculatePastryMaterialPrice(string variant_id, DatabaseContext context, KaizenTables kaizenTables)
        {
            double response = 0.0;

            Dictionary<string, InventorySubtractorInfo>? totalIngredientAmountConsumption = null;
            try { totalIngredientAmountConsumption = await DataParser.GetTotalIngredientAmountList(variant_id, context, kaizenTables); }
            catch { throw; }

            foreach (string ingredientId in totalIngredientAmountConsumption.Keys)
            {
                Item? currentItem = null;
                try { currentItem = await DataRetrieval.GetInventoryItemAsync(ingredientId, kaizenTables); }
                catch { throw; }

                InventorySubtractorInfo currentIngredientSubtractionInfo = totalIngredientAmountConsumption[ingredientId];
                double calculatedAmount = 0.0;
                try
                {
                    calculatedAmount = UnitConverter.ConvertByName(currentIngredientSubtractionInfo.Amount, currentIngredientSubtractionInfo.AmountQuantityType, currentIngredientSubtractionInfo.AmountUnit, currentItem.measurements) * currentItem.price;
                }
                catch
                {
                    calculatedAmount = currentIngredientSubtractionInfo.Amount * currentItem.price;
                }


                response += calculatedAmount;
            }
            return response;
        }

        //Recursive
        public static async Task<double> CalculateSubMaterialCost(MaterialIngredients data, DatabaseContext context, KaizenTables kaizenTables)
        {
            Materials? currentReferencedMaterial = null;
            try { currentReferencedMaterial = await context.Materials.Where(x => x.is_active == true && x.material_id == data.item_id).FirstAsync(); }
            catch { return 0.0; }
            if (currentReferencedMaterial == null) { return 0.0; }

            bool bothValidUnits = ValidUnits.IsUnitValid(data.amount_measurement) && ValidUnits.IsUnitValid(currentReferencedMaterial.amount_measurement);
            if (bothValidUnits == false) { return 0.0; }

            bool isSameQuantityUnit = ValidUnits.IsSameQuantityUnit(data.amount_measurement, currentReferencedMaterial.amount_measurement);
            if (isSameQuantityUnit == false) { return 0.0; }

            double costMultiplier = currentReferencedMaterial.amount_measurement.Equals(data.amount_measurement) ?
                data.amount / currentReferencedMaterial.amount :
                UnitConverter.ConvertByName(data.amount, ValidUnits.UnitQuantityMeasurement(currentReferencedMaterial.amount_measurement), data.amount_measurement, currentReferencedMaterial.amount_measurement) / currentReferencedMaterial.amount;
            double totalCost = 0.0;


            List<MaterialIngredients> currentReferencedMaterialIngredients = await context.MaterialIngredients.Where(x => x.is_active == true && x.material_id == currentReferencedMaterial.material_id).ToListAsync();
            foreach (MaterialIngredients materialIngredients in currentReferencedMaterialIngredients)
            {
                switch (materialIngredients.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        Item? currentMatIngRefItem = null;
                        try { currentMatIngRefItem = await kaizenTables.Item.Where(x => x.is_active == true && x.id == Convert.ToInt32(materialIngredients.item_id)).FirstAsync(); }
                        catch { continue; }

                        bool isInventoryItemMeasurementValid = ValidUnits.IsUnitValid(currentMatIngRefItem.measurements);
                        bool isInventoryItemQuantityUnitSame = ValidUnits.IsSameQuantityUnit(currentMatIngRefItem.measurements, materialIngredients.amount_measurement);
                        if (isInventoryItemMeasurementValid == false) { continue; }
                        if (isInventoryItemQuantityUnitSame == false) { continue; }

                        totalCost += currentMatIngRefItem.measurements.Equals(materialIngredients.amount_measurement) ?
                            (currentMatIngRefItem.price * materialIngredients.amount) * costMultiplier :
                            (currentMatIngRefItem.price * UnitConverter.ConvertByName(materialIngredients.amount, ValidUnits.UnitQuantityMeasurement(currentMatIngRefItem.measurements), materialIngredients.amount_measurement, currentMatIngRefItem.measurements)) * costMultiplier;
                        break;
                    case IngredientType.Material:
                        totalCost += await CalculateSubMaterialCost(materialIngredients, context, kaizenTables);
                        break;
                }
            }
            return totalCost;
        }
        public static async Task<double> CalculateSubMaterialCost(Ingredients data, DatabaseContext context, KaizenTables kaizenTables)
        {
            Materials? currentReferencedMaterial = null;
            try { currentReferencedMaterial = await context.Materials.Where(x => x.is_active == true && x.material_id == data.item_id).FirstAsync(); }
            catch { return 0.0; }
            if (currentReferencedMaterial == null) { return 0.0; }

            bool bothValidUnits = ValidUnits.IsUnitValid(data.amount_measurement) && ValidUnits.IsUnitValid(currentReferencedMaterial.amount_measurement);
            if (bothValidUnits == false) { return 0.0; }

            bool isSameQuantityUnit = ValidUnits.IsSameQuantityUnit(data.amount_measurement, currentReferencedMaterial.amount_measurement);
            if (isSameQuantityUnit == false) { return 0.0; }

            double costMultiplier = currentReferencedMaterial.amount_measurement.Equals(data.amount_measurement) ?
                data.amount / currentReferencedMaterial.amount :
                UnitConverter.ConvertByName(data.amount, ValidUnits.UnitQuantityMeasurement(currentReferencedMaterial.amount_measurement), data.amount_measurement, currentReferencedMaterial.amount_measurement) / currentReferencedMaterial.amount;
            double totalCost = 0.0;


            List<MaterialIngredients> currentReferencedMaterialIngredients = await context.MaterialIngredients.Where(x => x.is_active == true && x.material_id == currentReferencedMaterial.material_id).ToListAsync();
            foreach (MaterialIngredients materialIngredients in currentReferencedMaterialIngredients)
            {
                switch (materialIngredients.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        Item? currentMatIngRefItem = null;
                        try { currentMatIngRefItem = await kaizenTables.Item.Where(x => x.is_active == true && x.id == Convert.ToInt32(materialIngredients.item_id)).FirstAsync(); }
                        catch { continue; }

                        bool isInventoryItemMeasurementValid = ValidUnits.IsUnitValid(currentMatIngRefItem.measurements);
                        bool isInventoryItemQuantityUnitSame = ValidUnits.IsSameQuantityUnit(currentMatIngRefItem.measurements, materialIngredients.amount_measurement);
                        if (isInventoryItemMeasurementValid == false) { continue; }
                        if (isInventoryItemQuantityUnitSame == false) { continue; }

                        totalCost += currentMatIngRefItem.measurements.Equals(materialIngredients.amount_measurement) ?
                            (currentMatIngRefItem.price * materialIngredients.amount) * costMultiplier :
                            (currentMatIngRefItem.price * UnitConverter.ConvertByName(materialIngredients.amount, ValidUnits.UnitQuantityMeasurement(currentMatIngRefItem.measurements), materialIngredients.amount_measurement, currentMatIngRefItem.measurements)) * costMultiplier;
                        break;
                    case IngredientType.Material:
                        totalCost += await CalculateSubMaterialCost(materialIngredients, context, kaizenTables);
                        break;
                }
            }
            return totalCost;
        }
    }

    //Exceptions
    public class NotFoundInDatabaseException : Exception { public NotFoundInDatabaseException(string message) : base(message) { } }
    public class InvalidAmountMeasurementException : Exception { public InvalidAmountMeasurementException(string message) : base(message) { } }
    public class InvalidPastryMaterialIngredientTypeException : Exception { public InvalidPastryMaterialIngredientTypeException(string message) : base(message) { } }
}
