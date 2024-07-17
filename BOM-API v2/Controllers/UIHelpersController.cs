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
    [Route("ui-helpers/")]
    public class UIHelpersController: ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        public UIHelpersController(DatabaseContext databaseContext, KaizenTables kaizenTables) { _context = databaseContext; _kaizenTables = kaizenTables; }

        [Authorize]
        [HttpGet("valid-measurement-values")]
        public async Task<Dictionary<string, List<string>>> ValidMeasurementValues()
        {
            return ValidUnits.ValidMeasurementUnits();
        }
        [Authorize]
        [HttpGet("valid-item-types")]
        public async Task<string[]> ValidItemTypes()
        {
            return ["INV"];
        }
        [HttpGet("get-design-info/{designId}")]
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

            GetPastryMaterial parsedData = await DataParser.CreatePastryMaterialResponseFromDBRow(selectedDesignPastryMaterial, _context, _kaizenTables);

            response.pastry_material_id = parsedData.pastry_material_id;

            response.variants = new List<SubGetVariants>();
            SubGetVariants mainVariant = new SubGetVariants { variant_id = parsedData.pastry_material_id, variant_name = parsedData.main_variant_name, cost_estimate = parsedData.cost_estimate, in_stock = parsedData.ingredients_in_stock, add_ons = new List<SubGetAddOn>() };

            foreach (GetPastryMaterialAddOns currentPastryMaterialAddOn in parsedData.add_ons)
            {
                AddOns? referencedAddOns = null;
                try { referencedAddOns = await _kaizenTables.AddOns.Where(x => x.isActive == true && x.add_ons_id == currentPastryMaterialAddOn.add_ons_id).FirstAsync(); }
                catch { continue; }
                if (referencedAddOns == null) { continue; }

                SubGetAddOn newMainVariantAddOnsEntry = new SubGetAddOn();
                newMainVariantAddOnsEntry.pastry_material_add_on_id = currentPastryMaterialAddOn.pastry_material_add_on_id;
                newMainVariantAddOnsEntry.add_on_id = referencedAddOns.add_ons_id;
                newMainVariantAddOnsEntry.add_on_name = referencedAddOns.name;
                newMainVariantAddOnsEntry.amount = currentPastryMaterialAddOn.amount;
                newMainVariantAddOnsEntry.stock = referencedAddOns.quantity;
                newMainVariantAddOnsEntry.price = referencedAddOns.price;

                mainVariant.add_ons.Add(newMainVariantAddOnsEntry);
            }

            response.variants.Add(mainVariant);

            foreach (GetPastryMaterialSubVariant currentSubVariant in parsedData.sub_variants)
            {
                SubGetVariants newResponseSubVariantEntry = new SubGetVariants();
                newResponseSubVariantEntry.variant_id = currentSubVariant.pastry_material_sub_variant_id;
                newResponseSubVariantEntry.variant_name = currentSubVariant.sub_variant_name;
                newResponseSubVariantEntry.cost_estimate = currentSubVariant.cost_estimate;
                newResponseSubVariantEntry.in_stock = currentSubVariant.ingredients_in_stock;
                newResponseSubVariantEntry.add_ons = new List<SubGetAddOn>();

                foreach (GetPastryMaterialSubVariantAddOns currentSubVariantAddOn in currentSubVariant.sub_variant_add_ons)
                {
                    AddOns? referencedAddOns = null;
                    try { referencedAddOns = await _kaizenTables.AddOns.Where(x => x.isActive == true && x.add_ons_id == currentSubVariantAddOn.add_ons_id).FirstAsync(); }
                    catch { continue; }
                    if (referencedAddOns == null) { continue; }

                    SubGetAddOn newMainVariantAddOnsEntry = new SubGetAddOn();
                    newMainVariantAddOnsEntry.pastry_material_add_on_id = currentSubVariantAddOn.pastry_material_sub_variant_add_on_id;
                    newMainVariantAddOnsEntry.add_on_id = referencedAddOns.add_ons_id;
                    newMainVariantAddOnsEntry.add_on_name = referencedAddOns.name;
                    newMainVariantAddOnsEntry.amount = currentSubVariantAddOn.amount;
                    newMainVariantAddOnsEntry.stock = referencedAddOns.quantity;
                    newMainVariantAddOnsEntry.price = referencedAddOns.price;
                    
                    newResponseSubVariantEntry.add_ons.Add(newMainVariantAddOnsEntry);
                }

                response.variants.Add(newResponseSubVariantEntry);
            }

            return response;
        }

    }
}
