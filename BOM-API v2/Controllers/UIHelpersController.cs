using BillOfMaterialsAPI.Helpers;
using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using UnitsNet;
using ZstdSharp.Unsafe;

namespace BOM_API_v2.Controllers
{
    [ApiController]
    [Route("BOM/ui_helpers/")]
    [Authorize]
    public class UIHelpersController: ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        public UIHelpersController(DatabaseContext databaseContext, KaizenTables kaizenTables) { _context = databaseContext; _kaizenTables = kaizenTables; }

        [HttpGet("valid_measurement_values")]
        public async Task<Dictionary<string, List<string>>> ValidMeasurementValues()
        {
            return ValidUnits.ValidMeasurementUnits();
        }
        [HttpGet("valid_item_types")]
        public async Task<string[]> ValidItemTypes()
        {
            return ["MAT", "INV"];
        }
        [HttpGet("get_design_info/{designId}")]
        public async Task<GetDesignInfo> GetDesignInfo([FromRoute] string designId)
        {
            GetDesignInfo response = new GetDesignInfo();

            Designs? selectedDesign;
            PastryMaterials? selectedDesignPastryMaterial;

            string decodedId = designId;
            byte[]? byteArrEncodedId = null;
            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return response; }
            try { selectedDesign = await _context.Designs.Where(x => x.isActive == true && x.design_id.SequenceEqual(byteArrEncodedId)).FirstAsync(); }
            catch (Exception e) { return response; }

            try { selectedDesignPastryMaterial = await _context.PastryMaterials.Where(x => x.isActive == true && x.design_id.SequenceEqual(selectedDesign.design_id)).FirstAsync(); }
            catch (Exception e) { return response; }

            GetPastryMaterial parsedData = await CreatePastryMaterialResponseFromDBRow(selectedDesignPastryMaterial);

            response.pastry_material_id = parsedData.pastry_material_id;

            response.variants = new List<SubGetVariants>();
            response.variants.Add(new SubGetVariants {variant_id = parsedData.pastry_material_id, variant_name = parsedData.main_variant_name, cost_estimate = parsedData.cost_estimate, in_stock = parsedData.ingredients_in_stock });

            foreach (GetPastryMaterialSubVariant currentSubVariant in parsedData.sub_variants)
            {
                SubGetVariants newResponseSubVariantEntry = new SubGetVariants();
                newResponseSubVariantEntry.variant_id = currentSubVariant.pastry_material_sub_variant_id;
                newResponseSubVariantEntry.variant_name = currentSubVariant.sub_variant_name;
                newResponseSubVariantEntry.cost_estimate = currentSubVariant.cost_estimate;
                newResponseSubVariantEntry.in_stock = currentSubVariant.ingredients_in_stock;
                response.variants.Add(newResponseSubVariantEntry);
            }

            return response;
        }

        private async Task<GetPastryMaterial> CreatePastryMaterialResponseFromDBRow(PastryMaterials data)
        {
            GetPastryMaterial response = new GetPastryMaterial();
            response.design_id = Convert.ToBase64String(data.design_id);
            try { Designs? selectedDesign = await _context.Designs.Where(x => x.isActive == true && x.design_id.SequenceEqual(data.design_id)).Select(x => new Designs { display_name = x.display_name }).FirstAsync(); response.design_name = selectedDesign.display_name; }
            catch (Exception e) { response.design_name = "N/A"; }

            response.pastry_material_id = data.pastry_material_id;
            response.date_added = data.date_added;
            response.last_modified_date = data.last_modified_date;
            response.main_variant_name = data.main_variant_name;
            response.ingredients_in_stock = true;

            List<GetPastryMaterialIngredients> responsePastryMaterialList = new List<GetPastryMaterialIngredients>();
            List<GetPastryMaterialSubVariant> responsePastryMaterialSubVariants = new List<GetPastryMaterialSubVariant>();
            double calculatedCost = 0.0;

            List<Ingredients> currentPastryMaterialIngredients = await _context.Ingredients.Where(x => x.isActive == true && x.pastry_material_id == data.pastry_material_id).ToListAsync();

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
                            try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(currentIngredient.item_id)).FirstAsync(); }
                            catch { continue; }
                            if (currentInventoryItemI == null) { continue; }

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

            List<PastryMaterialSubVariants> currentPastryMaterialSubVariants = await _context.PastryMaterialSubVariants.Where(x => x.isActive == true && x.pastry_material_id == data.pastry_material_id).ToListAsync();
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

                List<PastryMaterialSubVariantIngredients> currentSubVariantIngredients = await _context.PastryMaterialSubVariantIngredients.Where(x => x.isActive == true && x.pastry_material_sub_variant_id == currentSubVariant.pastry_material_sub_variant_id).ToListAsync();
                List<SubGetPastryMaterialSubVariantIngredients> currentSubVariantIngredientList = new List<SubGetPastryMaterialSubVariantIngredients>();

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
                                try { currentInventoryItemI = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(currentSubVariantIngredient.item_id)).FirstAsync(); }
                                catch { continue; }
                                if (currentInventoryItemI == null) { continue; }

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
                                Materials? currentReferencedMaterial = await _context.Materials.Where(x => x.material_id == currentSubVariantIngredient.item_id && x.isActive == true).FirstAsync();
                                if (currentReferencedMaterial == null) { continue; }

                                newSubVariantIngredientListEntry.item_name = currentReferencedMaterial.material_name;
                                newSubVariantIngredientListEntry.item_id = currentReferencedMaterial.material_id;

                                List<MaterialIngredients> currentMaterialReferencedIngredients = await _context.MaterialIngredients.Where(x => x.material_id == currentSubVariantIngredient.item_id).ToListAsync();

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
                                    try { currentReferencedIngredientM = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(subIng.item_id)).FirstAsync(); }
                                    catch (Exception e) { Console.WriteLine("Error in retrieving " + subIng.item_id + " on inventory: " + e.GetType().ToString()); continue; }

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
                newSubVariantListRow.cost_estimate = estimatedCostSubVariant;
                newSubVariantListRow.sub_variant_ingredients = currentSubVariantIngredientList;

                responsePastryMaterialSubVariants.Add(newSubVariantListRow);
            }

            response.ingredients = responsePastryMaterialList;
            response.sub_variants = responsePastryMaterialSubVariants;
            response.cost_estimate = calculatedCost;

            return response;
        }
    }
}
