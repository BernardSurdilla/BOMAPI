﻿using BillOfMaterialsAPI.Helpers;
using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using JWTAuthentication.Authentication;
using LiveChat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BOM_API_v2.Controllers
{
    [ApiController]
    [Route("ui-helpers/")]
    public class UIHelpersController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        private readonly ILiveChatConnectionManager _liveChatConnectionManager;
        public UIHelpersController(DatabaseContext databaseContext, KaizenTables kaizenTables, ILiveChatConnectionManager connectionManager) { _context = databaseContext; _kaizenTables = kaizenTables; _liveChatConnectionManager = connectionManager; }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpGet("valid-measurement-values")]
        public Dictionary<string, List<string>> ValidMeasurementValues()
        {
            return ValidUnits.ValidMeasurementUnits();
        }
        [Authorize(Roles = UserRoles.Admin)]
        [HttpGet("valid-item-types")]
        public string[] ValidItemTypes()
        {
            return ValidFormInput.PastryMaterialIngredientTypes() ;
        }
        [Authorize(Roles = UserRoles.Admin)]
        [HttpGet("valid-ingredient-importance-values")]
        public Dictionary<string, int> ValidIngredientImportanceValues()
        {
            Dictionary<string, int> response = PastryMaterialIngredientImportanceCode.ValidIngredientImportanceCodes();

            return response;
        }
        [HttpGet("valid-design-shapes")]
        public string[] ValidDesignShapes()
        {
            return ValidFormInput.DesignShapes();
        }
        [HttpGet("valid-design-flavors")]
        public string[] ValidDesignFlavors()
        {
            return ValidFormInput.DesignFlavors();
        }

        [HttpGet("get-design-info/{designId}")]
        public async Task<GetDesignInfo> GetDesignInfo([FromRoute] Guid designId)
        {
            GetDesignInfo response = new GetDesignInfo();

            Designs? selectedDesign;
            PastryMaterials? selectedDesignPastryMaterial;

            try { selectedDesign = await _context.Designs.Where(x => x.is_active == true && x.design_id == designId).FirstAsync(); }
            catch (Exception e) { return response; }

            try { selectedDesignPastryMaterial = await _context.PastryMaterials.Where(x => x.is_active == true && x.design_id == selectedDesign.design_id).FirstAsync(); }
            catch (Exception e) { return response; }

            GetPastryMaterial parsedData = await DataParser.CreatePastryMaterialResponseFromDBRow(selectedDesignPastryMaterial, _context, _kaizenTables);

            response.pastryMaterialId = parsedData.pastryMaterialId;

            response.variants = new List<SubGetVariants>();
            SubGetVariants mainVariant = new SubGetVariants { variantId = parsedData.pastryMaterialId, variantName = parsedData.mainVariantName, costEstimate = parsedData.costEstimate, inStock = parsedData.ingredientsInStock, addOns = new List<SubGetAddOn>() };

            foreach (GetPastryMaterialAddOns currentPastryMaterialAddOn in parsedData.addOns)
            {
                AddOns? referencedAddOns = null;
                try { referencedAddOns = await _kaizenTables.AddOns.Where(x => x.add_ons_id == currentPastryMaterialAddOn.addOnsId).FirstAsync(); }
                catch { continue; }
                if (referencedAddOns == null) { continue; }

                SubGetAddOn newMainVariantAddOnsEntry = new SubGetAddOn();
                newMainVariantAddOnsEntry.pastryMaterialAddOnId = currentPastryMaterialAddOn.pastryMaterialAddOnId;
                newMainVariantAddOnsEntry.addOnId = referencedAddOns.add_ons_id;
                newMainVariantAddOnsEntry.addOnName = referencedAddOns.name;
                newMainVariantAddOnsEntry.amount = currentPastryMaterialAddOn.amount;
                newMainVariantAddOnsEntry.price = referencedAddOns.price;

                mainVariant.addOns.Add(newMainVariantAddOnsEntry);
            }

            response.variants.Add(mainVariant);

            foreach (GetPastryMaterialSubVariant currentSubVariant in parsedData.subVariants)
            {
                SubGetVariants newResponseSubVariantEntry = new SubGetVariants();
                newResponseSubVariantEntry.variantId = currentSubVariant.pastryMaterialSubVariantId;
                newResponseSubVariantEntry.variantName = currentSubVariant.subVariantName;
                newResponseSubVariantEntry.costEstimate = currentSubVariant.costEstimate;
                newResponseSubVariantEntry.inStock = currentSubVariant.ingredientsInStock;
                newResponseSubVariantEntry.addOns = new List<SubGetAddOn>();

                foreach (GetPastryMaterialSubVariantAddOns currentSubVariantAddOn in currentSubVariant.subVariantAddOns)
                {
                    AddOns? referencedAddOns = null;
                    try { referencedAddOns = await _kaizenTables.AddOns.Where(x => x.add_ons_id == currentSubVariantAddOn.addOnsId).FirstAsync(); }
                    catch { continue; }
                    if (referencedAddOns == null) { continue; }

                    SubGetAddOn newMainVariantAddOnsEntry = new SubGetAddOn();
                    newMainVariantAddOnsEntry.pastryMaterialAddOnId = currentSubVariantAddOn.pastryMaterialSubVariantAddOnId;
                    newMainVariantAddOnsEntry.addOnId = referencedAddOns.add_ons_id;
                    newMainVariantAddOnsEntry.addOnName = referencedAddOns.name;
                    newMainVariantAddOnsEntry.amount = currentSubVariantAddOn.amount;
                    newMainVariantAddOnsEntry.price = referencedAddOns.price;

                    newResponseSubVariantEntry.addOns.Add(newMainVariantAddOnsEntry);
                }

                response.variants.Add(newResponseSubVariantEntry);
            }

            return response;
        }

        [HttpGet("live-chat/online-admins")]
        public async Task<List<ChatConnection>> GetLiveChatAdminsOnline()
        {
            List<ChatConnection> response = new List<ChatConnection>();

            List<LiveChat.ConnectionInfo> connectionInfos = new List<LiveChat.ConnectionInfo>();

            connectionInfos.AddRange(_liveChatConnectionManager.GetAllAdminConnections());
            connectionInfos.AddRange(_liveChatConnectionManager.GetAllManagerConnections());

            connectionInfos.GroupBy(x => x.ConnectionId).Select(g => g.First()).ToList();

            foreach (LiveChat.ConnectionInfo? connectionInfo in connectionInfos)
            {
                if (connectionInfo == null) continue;
                response.Add(new ChatConnection
                {
                    connection_id = connectionInfo.ConnectionId,
                    account_id = connectionInfo.AccountId,
                    name = connectionInfo.Name,
                    role = connectionInfo.Claims == null ? "Customer" : connectionInfo.Claims.FirstOrDefault()
                });
            }

            response = response.GroupBy(x => x.account_id).Select(x => x.First()).ToList();
            return response;
        }
        [HttpGet("live-chat/online-users")]
        public async Task<List<ChatConnection>> GetLiveChatUsersOnline()
        {
            List<ChatConnection> response = new List<ChatConnection>();

            List<LiveChat.ConnectionInfo> connectionInfos = new List<LiveChat.ConnectionInfo>();

            connectionInfos.AddRange(_liveChatConnectionManager.GetAllConnections());

            foreach (LiveChat.ConnectionInfo? connectionInfo in connectionInfos)
            {
                if (connectionInfo == null) continue;
                response.Add(new ChatConnection
                {
                    connection_id = connectionInfo.ConnectionId,
                    account_id = connectionInfo.AccountId,
                    name = connectionInfo.Name,
                    role = connectionInfo.Claims == null || connectionInfo.Claims.IsNullOrEmpty() ? "Customer" : connectionInfo.Claims.FirstOrDefault()
                });
            }
            response = response.GroupBy(x => x.account_id).Select(x => x.First()).ToList();
            return response;
        }
    }

    public class ChatConnection
    {
        public string connection_id { get; set; }
        public string account_id { get; set; }
        public string? name { get; set; }
        public string? role { get; set; }
    }
}
