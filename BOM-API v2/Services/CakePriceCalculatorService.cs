using BillOfMaterialsAPI.Helpers;
using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.EntityFrameworkCore;
using UnitsNet;

namespace BOM_API_v2.Services
{
    public class CakePriceCalculatorService : ICakePriceCalculator
    {
        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        public CakePriceCalculatorService(DatabaseContext dbContext, KaizenTables kaizenTables) { _context = dbContext; _kaizenTables = kaizenTables; }

        public async Task<double> CalculateSubMaterialCost(MaterialIngredients data)
        {
            Materials? currentReferencedMaterial = null;
            try { currentReferencedMaterial = await _context.Materials.Where(x => x.isActive == true && x.material_id == data.item_id).FirstAsync(); }
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


            List<MaterialIngredients> currentReferencedMaterialIngredients = await _context.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentReferencedMaterial.material_id).ToListAsync();
            foreach (MaterialIngredients materialIngredients in currentReferencedMaterialIngredients)
            {
                switch (materialIngredients.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        Item? currentMatIngRefItem = null;
                        try { currentMatIngRefItem = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(materialIngredients.item_id)).FirstAsync(); }
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
                        totalCost += await CalculateSubMaterialCost(materialIngredients);
                        break;
                }
            }
            return totalCost;
        }
        public async Task<double> CalculateSubMaterialCost(Ingredients data)
        {
            Materials? currentReferencedMaterial = null;
            try { currentReferencedMaterial = await _context.Materials.Where(x => x.isActive == true && x.material_id == data.item_id).FirstAsync(); }
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


            List<MaterialIngredients> currentReferencedMaterialIngredients = await _context.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentReferencedMaterial.material_id).ToListAsync();
            foreach (MaterialIngredients materialIngredients in currentReferencedMaterialIngredients)
            {
                switch (materialIngredients.ingredient_type)
                {
                    case IngredientType.InventoryItem:
                        Item? currentMatIngRefItem = null;
                        try { currentMatIngRefItem = await _kaizenTables.Item.Where(x => x.isActive == true && x.id == Convert.ToInt32(materialIngredients.item_id)).FirstAsync(); }
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
                        totalCost += await CalculateSubMaterialCost(materialIngredients);
                        break;
                }
            }
            return totalCost;
        }

    }
}
