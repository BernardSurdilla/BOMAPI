using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Helpers;

using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using System.Diagnostics;
using BOM_API_v2.Services;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore.Internal;
using UnitsNet;
using System.Text.Json;


namespace BOM_API_v2.Controllers
{
    [ApiController]
    [Route("designs/")]
    public class DesignsController : ControllerBase
    {
        private readonly DatabaseContext _databaseContext;
        private readonly KaizenTables _kaizenTables;
        private readonly IActionLogger _actionLogger;

        public DesignsController(DatabaseContext databaseContext, IActionLogger actionLogger, KaizenTables kaizenTables) { _databaseContext = databaseContext; _actionLogger = actionLogger; _kaizenTables = kaizenTables; }

        [HttpGet]
        public async Task<List<GetDesign>> GetAllDesigns(int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {
            IQueryable<Designs> dbQuery = _databaseContext.Designs.Where(x => x.isActive == true);

            List<Designs> current_design_records = new List<Designs>();
            List<GetDesign> response = new List<GetDesign>();

            switch (sortBy)
            {
                case "design_id":
                    switch (sortOrder)
                    {
                        case "DESC":
                            dbQuery = dbQuery.OrderByDescending(x => x.design_id);
                            break;
                        default:
                            dbQuery = dbQuery.OrderBy(x => x.design_id);
                            break;
                    }
                    break;
                case "display_name":
                    switch (sortOrder)
                    {
                        case "DESC":
                            dbQuery = dbQuery.OrderByDescending(x => x.display_name);
                            break;
                        default:
                            dbQuery = dbQuery.OrderBy(x => x.display_name);
                            break;
                    }
                    break;
                case "display_picture_url":
                    switch (sortOrder)
                    {
                        case "DESC":
                            dbQuery = dbQuery.OrderByDescending(x => x.display_picture_url);
                            break;
                        default:
                            dbQuery = dbQuery.OrderBy(x => x.display_picture_url);
                            break;
                    }
                    break;
            }

            //Paging algorithm
            if (page == null) { current_design_records = await dbQuery.ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < Page.DefaultNumberOfEntriesPerPage ? Page.DefaultNumberOfEntriesPerPage : record_per_page.Value;
                int current_page = page.Value < Page.DefaultStartingPageNumber ? Page.DefaultStartingPageNumber : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                current_design_records = await dbQuery.Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }


            foreach (Designs currentDesign in  current_design_records)
            {
                GetDesign newResponseEntry = await CreateGetDesignResponseFromDbRow(currentDesign);

                response.Add(newResponseEntry);
            }

            await _actionLogger.LogAction(User, "GET", "All Design");
            return response;
        }
        [HttpGet("tags/")]
        public async Task<List<GetDesignTag>> GetAllDesignTags(int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {
            IQueryable<DesignTags> dbQuery = _databaseContext.DesignTags.Where(x => x.isActive == true);

            List<DesignTags> current_design_records = new List<DesignTags>();
            List<GetDesignTag> response = new List<GetDesignTag>();

            switch (sortBy)
            {
                case "design_tag_id":
                    switch (sortOrder)
                    {
                        case "DESC":
                            dbQuery = dbQuery.OrderByDescending(x => x.design_tag_id);
                            break;
                        default:
                            dbQuery = dbQuery.OrderBy(x => x.design_tag_id);
                            break;
                    }
                    break;
                case "design_tag_name":
                    switch (sortOrder)
                    {
                        case "DESC":
                            dbQuery = dbQuery.OrderByDescending(x => x.design_tag_name);
                            break;
                        default:
                            dbQuery = dbQuery.OrderBy(x => x.design_tag_name);
                            break;
                    }
                    break;
            }

            //Paging algorithm
            if (page == null) { current_design_records = await dbQuery.ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < Page.DefaultNumberOfEntriesPerPage ? Page.DefaultNumberOfEntriesPerPage : record_per_page.Value;
                int current_page = page.Value < Page.DefaultStartingPageNumber ? Page.DefaultStartingPageNumber : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                current_design_records = await dbQuery.Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            foreach (DesignTags currentDesignTag in current_design_records)
            {
                GetDesignTag newResponseEntry = new GetDesignTag();
                newResponseEntry.design_tag_id = currentDesignTag.design_tag_id;
                newResponseEntry.design_tag_name = currentDesignTag.design_tag_name;
                response.Add(newResponseEntry);
            }

            await _actionLogger.LogAction(User, "GET", "All Design tags");
            return response;
        }
        [HttpGet("{designId}")]
        public async Task<GetDesign> GetSpecificDesign([FromRoute]string designId)
        {
            Designs? selectedDesign;
            string decodedId = designId;
            byte[]? byteArrEncodedId = null;
            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return new GetDesign(); }

            try { selectedDesign = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id.SequenceEqual(byteArrEncodedId)).FirstAsync(); }
            catch (Exception e) { return new GetDesign(); }

            GetDesign response = await CreateGetDesignResponseFromDbRow(selectedDesign);
            /*
            List<DesignTagsForCakes> tagsForCurrentCake = await _databaseContext.DesignTagsForCakes.Include(x => x.DesignTags).Where(x => x.isActive == true && x.design_id.SequenceEqual(selectedDesign.design_id)).ToListAsync();

            DesignImage? currentDesignImage = null;
            try { currentDesignImage = await _databaseContext.DesignImage.Where(x => x.isActive == true && x.design_id == selectedDesign.design_id).FirstAsync(); }
            catch (Exception e) {  }

            GetDesign response = new GetDesign();
            response.design_id = selectedDesign.design_id;
            response.display_name = selectedDesign.display_name;
            response.cake_description = selectedDesign.cake_description;
            response.design_tags = new List<GetDesignTag>();

            if (currentDesignImage != null) { response.display_picture_data = currentDesignImage.picture_data; }
            else { response.display_picture_data = null; }

            foreach (DesignTagsForCakes currentTag in tagsForCurrentCake)
            {
                DesignTags? referencedTag = null;
                try { referencedTag = currentTag.DesignTags; }
                catch { continue; }
                
                if (referencedTag != null)
                {
                    response.design_tags.Add(new GetDesignTag { design_tag_id = referencedTag.design_tag_id, design_tag_name = referencedTag.design_tag_name});
                }
            }
            */
            await _actionLogger.LogAction(User, "GET", "Design " + decodedId);
            return response;
        }
        [HttpGet("tags/{design-tag-id}")]
        public async Task<GetTag> GetSpecificTag([FromRoute] Guid design_tag_id)
        {
            DesignTags? selectedTag = null;
            try { selectedTag = await _databaseContext.DesignTags.Where(x => x.isActive == true && x.design_tag_id == design_tag_id).FirstAsync(); }
            catch (Exception e) { return new GetTag(); }

            GetTag response = new GetTag();
            response.design_tag_name = selectedTag.design_tag_name;

            await _actionLogger.LogAction(User, "GET", "Design tag " + selectedTag.design_tag_id);
            return response;
        }
        [HttpGet("with-tags/{*tags}")]
        public async Task<List<GetDesign>> GetDesignsWithTag([FromRoute]string tags )
        {
            List<GetDesign> response = new List<GetDesign>();
            string decodedIds = tags;

            try
            {
                decodedIds = Uri.UnescapeDataString(decodedIds);
            }
            catch(Exception e) { return(response); }

            string[] design_ids = decodedIds.Split("/");

            List<List<string>> designIdWithTags = new List<List<string>>();
            foreach (string design_id in design_ids)
            {
                DesignTags? currentTag;
                try { currentTag = await _databaseContext.DesignTags.Where(x => x.isActive == true && x.design_tag_id.ToString() == design_id).FirstAsync(); }
                catch (Exception e) { return new List<GetDesign>(); }

                List<DesignTagsForCakes> tagsForCakes = await _databaseContext.DesignTagsForCakes.Where(x => x.isActive == true && x.design_tag_id == currentTag.design_tag_id).ToListAsync();
                List<string> designIdsWithCurrentTag = new List<string>();
                foreach (DesignTagsForCakes designTagsForCakes in tagsForCakes)
                {
                    designIdsWithCurrentTag.Add(Convert.ToBase64String(designTagsForCakes.design_id));
                }
                designIdWithTags.Add(designIdsWithCurrentTag);
            }

            List<string>? cakeIdList = null;
            foreach (List<string> currentIdList in designIdWithTags)
            {
                if (cakeIdList == null)
                {
                    cakeIdList = currentIdList;
                }
                else
                {
                    cakeIdList = cakeIdList.Intersect(currentIdList).ToList();
                }
            }

            foreach (string currentCakeId in cakeIdList)
            {
                Designs? selectedDesign = null;
                byte[] encodedId = Convert.FromBase64String(currentCakeId);
                try { selectedDesign = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id.SequenceEqual(encodedId)).FirstAsync(); }
                catch (Exception e) { continue; }

                response.Add(await CreateGetDesignResponseFromDbRow(selectedDesign));
            }

            await _actionLogger.LogAction(User, "GET", "All Design with specific tags ");
            return response;

        }

        [HttpGet("{designId}/pastry-material")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<GetPastryMaterial> GetSpecificPastryMaterialByDesignId([FromRoute] string designId)
        {
            Designs? selectedDesign;
            string decodedId = designId;
            byte[]? byteArrEncodedId = null;
            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return new GetPastryMaterial(); }
            try { selectedDesign = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id.SequenceEqual(byteArrEncodedId)).FirstAsync(); }
            catch (Exception e) { return new GetPastryMaterial(); }

            PastryMaterials? currentPastryMat = null;
            try { currentPastryMat = await _databaseContext.PastryMaterials.Where(x => x.isActive == true && x.design_id.SequenceEqual(selectedDesign.design_id)).FirstAsync(); }
            catch (Exception e) { return new GetPastryMaterial(); }


            List<Ingredients> ingredientsForCurrentMaterial = await _databaseContext.Ingredients.Where(x => x.isActive == true && x.pastry_material_id == currentPastryMat.pastry_material_id).ToListAsync();
            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients

            GetPastryMaterial response = await CreatePastryMaterialResponseFromDBRow(currentPastryMat);

            await _actionLogger.LogAction(User, "GET", "Pastry Material " + currentPastryMat.pastry_material_id);
            return response;

        }

        [HttpGet("without-pastry-material")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<List<GetDesignWithoutPastryMaterial>> GetDesignsWithoutPastryMaterial()
        {
            List<GetDesignWithoutPastryMaterial> response = new List<GetDesignWithoutPastryMaterial>();

            List<Designs> dbResp = await _databaseContext.Designs.Where(x => x.isActive == true && _databaseContext.PastryMaterials.Where(x=> x.isActive == true).Select(x => x.design_id).Contains(x.design_id) == false).Select(x => new Designs { design_id = x.design_id, display_name = x.display_name}).ToListAsync();
            foreach (Designs design in dbResp)
            {
                GetDesignWithoutPastryMaterial newResponseRow = new GetDesignWithoutPastryMaterial();
                newResponseRow.display_name = design.display_name;
                newResponseRow.design_id = design.design_id;
                response.Add(newResponseRow);
            }
                
            return response;
        }

        /*
        [HttpGet("with_pastry_material")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<List<GetDesignWithPastryMaterial>> GetDesignsWithPastryMaterial()
        {
            List<GetDesignWithPastryMaterial> response = new List<GetDesignWithPastryMaterial>();

            //var dbResp = await _databaseContext.Designs.Where(x => x.isActive == true).LeftJoin()

            return response;
            /*

            List<Designs> dbResp = await _databaseContext.Designs.Where(x => x.isActive == true && _databaseContext.PastryMaterials.Where(x => x.isActive == true).Select(x => x.design_id).Contains(x.design_id) == true).Select(x => new Designs { design_id = x.design_id, display_name = x.display_name }).ToListAsync();
            foreach (Designs design in dbResp)
            {
                GetDesignWithoutPastryMaterial newResponseRow = new GetDesignWithoutPastryMaterial();
                newResponseRow.display_name = design.display_name;
                newResponseRow.design_id = design.design_id;
                response.Add(newResponseRow);
            }

        }
        */
        private async Task<GetDesign> CreateGetDesignResponseFromDbRow(Designs data)
        {
            GetDesign response = new GetDesign();

            response.design_id = data.design_id;
            response.display_name = data.display_name;
            response.design_picture_url = data.display_picture_url;
            response.cake_description = data.cake_description;
            response.design_tags = new List<GetDesignTag>();
            response.design_add_ons = new List<GetDesignAddOns>();

            List<DesignTagsForCakes> cakeTags = await _databaseContext.DesignTagsForCakes.Include(x => x.DesignTags).Where(x => x.isActive == true && x.design_id == data.design_id && x.DesignTags.isActive == true).ToListAsync();
            List<DesignAddOns> cakeAddOns = await _kaizenTables.DesignAddOns.Include(x => x.AddOns).Where(x => x.isActive == true && x.AddOns.isActive == true && x.design_id.SequenceEqual(data.design_id)).ToListAsync();
            DesignImage? image;
            try { image = await _databaseContext.DesignImage.Where(x => x.isActive == true && x.design_id == data.design_id).FirstAsync(); }
            catch { image = null; }

            foreach (DesignTagsForCakes currentTag in cakeTags)
            {
                if (currentTag.DesignTags != null)
                {
                    response.design_tags.Add(new GetDesignTag { design_tag_id = currentTag.DesignTags.design_tag_id, design_tag_name = currentTag.DesignTags.design_tag_name });
                }
            }
            foreach (DesignAddOns currentAddOn in cakeAddOns)
            {
                response.design_add_ons.Add(new GetDesignAddOns { add_ons_id = currentAddOn.add_ons_id, add_on_name = currentAddOn.add_on_name, design_add_on_id = currentAddOn.design_add_on_id, price = currentAddOn.price, quantity = currentAddOn.quantity });
            }
            if (image != null) { response.display_picture_data = image.picture_data; }
            else { response.display_picture_data = null; };

            return response;
        }
        
        [HttpPost]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewDesign(PostDesign input)
        {
            byte[] newEntryId = Guid.NewGuid().ToByteArray();
            DateTime currentTime = DateTime.Now;

            Designs newEntry = new Designs();
            newEntry.design_id = newEntryId;
            newEntry.display_name = input.display_name;
            newEntry.display_picture_url = input.display_picture_url;
            newEntry.cake_description = input.cake_description;

            newEntry.isActive = true;

            List<DesignTagsForCakes> newTagRelationships = new List<DesignTagsForCakes>();
            List<DesignAddOns> newAddOnsRelationships = new List<DesignAddOns>();
            if (input.design_tag_ids != null)
            {
                foreach (Guid tagId in input.design_tag_ids)
                {
                    DesignTags? selectedDesignTag = await _databaseContext.DesignTags.FindAsync(tagId);
                    if (selectedDesignTag == null) { continue; }
                    else
                    {
                        DesignTagsForCakes newTagReference = new DesignTagsForCakes();
                        newTagReference.design_tags_for_cake_id = new Guid();
                        newTagReference.design_id = newEntry.design_id;
                        newTagReference.design_tag_id = selectedDesignTag.design_tag_id;
                        newTagReference.isActive = true;

                        newTagRelationships.Add(newTagReference);
                    }
                }
            }
            if (input.design_add_ons != null)
            {
                foreach (PostDesignAddOns currentEntryAddOnInfo in input.design_add_ons)
                {
                    AddOns? selectedAddOn = null;
                    try { selectedAddOn = await _kaizenTables.AddOns.Where(x => x.isActive == true && x.add_ons_id == currentEntryAddOnInfo.add_ons_id).FirstAsync(); }
                    catch { continue; }
                    if (selectedAddOn == null) { continue; }
                    else
                    {
                        DesignAddOns newAddOnConnection = new DesignAddOns();
                        newAddOnConnection.design_id = newEntryId;
                        newAddOnConnection.add_ons_id = selectedAddOn.add_ons_id;
                        newAddOnConnection.add_on_name = currentEntryAddOnInfo.add_on_name;
                        newAddOnConnection.quantity = currentEntryAddOnInfo.quantity;
                        newAddOnConnection.price = currentEntryAddOnInfo.price;

                        newAddOnConnection.last_modified_date = currentTime;
                        newAddOnConnection.date_added = currentTime;
                        newAddOnConnection.isActive = true;

                        newAddOnsRelationships.Add(newAddOnConnection);
                    }
                }
            }
            DesignImage? newDesignImage = null;
            if (input.display_picture_data != null)
            {
                newDesignImage = new DesignImage();
                newDesignImage.design_id = newEntry.design_id;
                newDesignImage.design_picture_id = new Guid();
                newDesignImage.picture_data = input.display_picture_data;
                newDesignImage.isActive = true;
            }

            _databaseContext.Designs.Add(newEntry);
            _databaseContext.SaveChanges();

            if (newTagRelationships.IsNullOrEmpty() == false) { await _databaseContext.DesignTagsForCakes.AddRangeAsync(newTagRelationships); await _databaseContext.SaveChangesAsync(); }
            if (newAddOnsRelationships.IsNullOrEmpty() == false) { await _kaizenTables.DesignAddOns.AddRangeAsync(newAddOnsRelationships); await _kaizenTables.SaveChangesAsync(); }
            if (newDesignImage != null) { await _databaseContext.DesignImage.AddAsync(newDesignImage); await _databaseContext.SaveChangesAsync(); }

            await _actionLogger.LogAction(User, "POST", "Add new design " + newEntryId.ToString());
            return Ok(new { message = "Design " + Convert.ToBase64String(newEntryId) + " added", id = Convert.ToBase64String(newEntryId) });
        }
        [HttpPost("tags/")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewTags(PostTags input)
        {
            if (input == null) { return BadRequest( new {mesage = "Invalid input"}); }

            DesignTags newTags = new DesignTags();
            newTags.design_tag_id = new Guid();
            newTags.design_tag_name = input.design_tag_name;
            newTags.isActive = true;

            await _databaseContext.DesignTags.AddAsync(newTags);
            await _databaseContext.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add new tag " + newTags.design_tag_name.ToString());
            return Ok(new {message = "Tag " + newTags.design_tag_id + " created"});
        }
        [HttpPost("{designId}/add-ons/")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewAddOns([FromBody]PostDesignAddOns input, [FromRoute] string designId)
        {
            Designs? selectedDesign;
            string decodedId = designId;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Cannot convert design id on route to string" }); }

            try { selectedDesign = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (Exception e) { return NotFound(new { message = "Design id not found" }); }

            if (input == null) { return BadRequest(new { message = "Input is null" }); }
            AddOns? selectedAddOn = null;
            try { selectedAddOn = await _kaizenTables.AddOns.Where(x => x.isActive == true && x.add_ons_id == input.add_ons_id).FirstAsync(); }
            catch { return NotFound(new { message = "Add on with the specified id not found" }); ; }

            DateTime currentTime = DateTime.Now;
            DesignAddOns newAddOnConnection = new DesignAddOns();
            newAddOnConnection.design_id = byteArrEncodedId;
            newAddOnConnection.add_ons_id = selectedAddOn.add_ons_id;
            newAddOnConnection.add_on_name = input.add_on_name;
            newAddOnConnection.quantity = input.quantity;
            newAddOnConnection.price = input.price;

            newAddOnConnection.last_modified_date = currentTime;
            newAddOnConnection.date_added = currentTime;
            newAddOnConnection.isActive = true;

            await _kaizenTables.DesignAddOns.AddAsync(newAddOnConnection);
            await _kaizenTables.SaveChangesAsync();

            return Ok(new {message = "New add on added for " + decodedId});
        }

        [HttpPut("{designId}/tags")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddDesignTags(PostDesignTags input, [FromRoute] string designId)
        {
            Designs? selectedDesign;
            string decodedId = designId;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Cannot convert design id on route to string" }); }

            try { selectedDesign = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (Exception e) { return NotFound(new { message = "Design id not found" }); }

            if (input == null) { return BadRequest(new { message = "Input is null" }); }
            if (input.design_tag_ids.IsNullOrEmpty()) { return BadRequest(new { message = "No tag ids in the input body" }); }

            foreach (Guid tagId in input.design_tag_ids)
            {
                DesignTags referencedTag;
                try { referencedTag = await _databaseContext.DesignTags.Where(x => x.isActive == true && x.design_tag_id == tagId).FirstAsync(); }
                catch (InvalidOperationException e) { return BadRequest(new { message = "The tag with the id " + tagId + " does not exist" }); }
                catch (Exception e) { return StatusCode(500, new { message = e.GetType().ToString() }); }

                DesignTagsForCakes? currentDesignTagConnection = null;
                try
                {
                    currentDesignTagConnection = await _databaseContext.DesignTagsForCakes.Where(x => x.design_id.SequenceEqual(byteArrEncodedId) == true && x.design_tag_id == referencedTag.design_tag_id).FirstAsync();

                    if (currentDesignTagConnection.isActive == false)
                    {
                        _databaseContext.DesignTagsForCakes.Update(currentDesignTagConnection);
                        currentDesignTagConnection.isActive = true;
                    }
                    else { continue; }
                }
                catch
                {
                    DesignTagsForCakes newDesignTag = new DesignTagsForCakes();
                    newDesignTag.isActive = true;
                    newDesignTag.design_tags_for_cake_id = new Guid();
                    newDesignTag.design_id = selectedDesign.design_id;
                    newDesignTag.design_tag_id = referencedTag.design_tag_id;
                    _databaseContext.DesignTagsForCakes.Add(newDesignTag);
                }
            }
            await _databaseContext.SaveChangesAsync();
            return Ok(new { message = "Tags inserted to " + selectedDesign.design_id.ToString() });
        }

        [HttpPatch("{designId}/")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdateDesign(PostDesign input, [FromRoute]string designId)
        {
            string decodedId = designId;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new {message="Invalid format in the designId value in route"}); }

            Designs? foundEntry = null;
            try { foundEntry = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (InvalidOperationException e) { return NotFound(new { message = "Design with the specified id not found" }); }
            catch (Exception e) { return BadRequest(new { message = "An unspecified error occured when retrieving the data" }); }

            _databaseContext.Designs.Update(foundEntry);
            foundEntry.display_name = input.display_name;
            foundEntry.cake_description = input.cake_description;
            foundEntry.display_picture_url = input.display_picture_url;

            List<DesignTagsForCakes> allDesignTagsForCakes = await _databaseContext.DesignTagsForCakes.Where(x => x.isActive == true && x.design_id == foundEntry.design_id).ToListAsync();
            List<Guid> normalizedInputTagIdList = input.design_tag_ids != null ? input.design_tag_ids.Distinct().ToList() : new List<Guid>();

            foreach (Guid currentTagId in normalizedInputTagIdList)
            {
                DesignTags referencedTag;
                try { referencedTag = await _databaseContext.DesignTags.Where(x => x.isActive == true && x.design_tag_id == currentTagId).FirstAsync(); }
                catch (InvalidOperationException e) { return BadRequest(new { message = "The tag with the id " + currentTagId + " does not exist" }); }
                catch (Exception e) { return StatusCode(500, new { message = e.GetType().ToString() }); }

                if (allDesignTagsForCakes.Where(x => x.design_tag_id == currentTagId).IsNullOrEmpty())
                {
                    DesignTagsForCakes newTagConnection = new DesignTagsForCakes();
                    newTagConnection.design_tags_for_cake_id = new Guid();
                    newTagConnection.design_id = byteArrEncodedId;
                    newTagConnection.design_tag_id = currentTagId;
                    newTagConnection.isActive = true;

                    await _databaseContext.DesignTagsForCakes.AddAsync(newTagConnection);
                }
            }
            if (input.display_picture_data != null)
            {
                DesignImage? designImage = null;
                try { designImage = await _databaseContext.DesignImage.Where(x => x.isActive == true && x.design_id.SequenceEqual(byteArrEncodedId)).FirstAsync(); }
                catch { }
                if (designImage == null)
                {
                    designImage = new DesignImage();
                    designImage.design_id = byteArrEncodedId;
                    designImage.design_picture_id = new Guid();
                    designImage.picture_data = input.display_picture_data;
                    designImage.isActive = true;
                    _databaseContext.DesignImage.Add(designImage);
                }
                else
                {
                    designImage.picture_data = input.display_picture_data;
                    _databaseContext.DesignImage.Update(designImage);
                }
            }

            await _databaseContext.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update design " + decodedId);
            return Ok(new { message = "Design " + designId.ToString() + " updated" });
        }
        [HttpPatch("tags/{design-tag-id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdateTag(PostTags input, [FromRoute] Guid design_tag_id)
        {
            DesignTags? selectedDesignTag;
            try { selectedDesignTag = await _databaseContext.DesignTags.Where(x => x.isActive == true && x.design_tag_id == design_tag_id).FirstAsync(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = "Specified design tag with the id " + design_tag_id + " does not exist" }); }
            catch (Exception e) { return BadRequest(new { message = "An unspecified error occured when retrieving the data" }); }

            _databaseContext.DesignTags.Update(selectedDesignTag);
            selectedDesignTag.design_tag_name = input.design_tag_name;
            await _databaseContext.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update design tag " + selectedDesignTag.design_tag_id);
            return Ok(new { message = "Design tag " + selectedDesignTag.design_tag_id + " updated" });
        }
        [HttpPatch("{designId}/add-ons/{design-add-on-id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdateDesignAddOn(PatchDesignAddOns input, [FromRoute] string designId, [FromRoute] int design_add_on_id)
        {
            string decodedId = designId;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Invalid format in the designId value in route" }); }

            AddOns? selectedAddOn = null;
            try { selectedAddOn = await _kaizenTables.AddOns.Where(x => x.isActive == true && x.add_ons_id == input.add_ons_id).FirstAsync(); }
            catch { return NotFound(new { message = "Design add on with the id " + design_add_on_id + " not found" }); }
            if (selectedAddOn == null) { return NotFound(new { message = "Design add on with the id " + design_add_on_id + " not found" }); }

            DesignAddOns? selectedAddOnForDesign = null;
            try { selectedAddOnForDesign = await _kaizenTables.DesignAddOns.Where(x => x.isActive == true && x.design_add_on_id == design_add_on_id && x.design_id.SequenceEqual(byteArrEncodedId)).FirstAsync(); }
            catch { return NotFound(new { message = "Design add on with the id " + design_add_on_id + " not found" }); }

            _kaizenTables.DesignAddOns.Update(selectedAddOnForDesign);
            selectedAddOnForDesign.add_ons_id = selectedAddOn.add_ons_id;
            selectedAddOnForDesign.add_on_name = input.add_on_name;
            selectedAddOnForDesign.quantity = input.quantity;
            selectedAddOnForDesign.price = input.price;
            selectedAddOnForDesign.last_modified_date = DateTime.Now;
            await _kaizenTables.SaveChangesAsync();

            return Ok(new { message = "Design add on for " + decodedId + " id " + selectedAddOnForDesign.design_add_on_id + " updated" });
        }

        [HttpDelete("{designId}/")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeleteDesign([FromRoute] string designId)
        {
            string decodedId = designId;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Invalid format in the designId value in route" }); }

            Designs? foundEntry = null;
            try { foundEntry = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (InvalidOperationException e) { return NotFound(new { message = "Design with the specified id not found" }); }
            catch (Exception e) { return BadRequest(new { message = "An unspecified error occured when retrieving the data" }); }

            _databaseContext.Designs.Update(foundEntry);
            foundEntry.isActive = false;
            await _databaseContext.SaveChangesAsync();
            

            await _actionLogger.LogAction(User, "DELETE", "Delete design " + decodedId);
            return Ok(new { message = "Design " + decodedId + " deleted" });
        }
        [HttpDelete("tags/{design-tag-id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeleteDesignTag(Guid design_tag_id)
        {
            DesignTags? selectedDesignTag;
            try { selectedDesignTag = await _databaseContext.DesignTags.Where(x => x.isActive == true && x.design_tag_id == design_tag_id).FirstAsync(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = "Specified design tag with the id " + design_tag_id + " does not exist" }); }
            catch (Exception e) { return BadRequest(new { message = "An unspecified error occured when retrieving the data" }); }

            _databaseContext.DesignTags.Update(selectedDesignTag);
            selectedDesignTag.isActive = false;

            await _databaseContext.SaveChangesAsync();
            await _actionLogger.LogAction(User, "DELETE", "Delete design tag " + selectedDesignTag.design_tag_id);
            return Ok(new { message = "Design " + selectedDesignTag.design_tag_id + " deleted" });
        }
        [HttpDelete("{designId}/tags")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> RemoveDesignTag([FromRoute]string designId, [FromBody] List<Guid> tag_ids)
        {
            string decodedId = designId;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Invalid format in the designId value in route" }); }

            Designs? foundEntry = null;
            try { foundEntry = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (InvalidOperationException e) { return NotFound(new { message = "Design with the specified id not found" }); }

            List<DesignTagsForCakes> currentTags = await _databaseContext.DesignTagsForCakes.Where(x => x.isActive == true && x.design_id == foundEntry.design_id).ToListAsync();
            List<Guid> normalizedCakeTagIds = tag_ids.Distinct().ToList();
            foreach (Guid tagId in normalizedCakeTagIds)
            {
                DesignTagsForCakes? currentReferencedTag = currentTags.Where(x => x.design_tag_id == tagId).FirstOrDefault();
                if (currentReferencedTag != null)
                {
                    _databaseContext.DesignTagsForCakes.Update(currentReferencedTag);
                    currentReferencedTag.isActive = false;
                }
            }
            await _databaseContext.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Tags for " + foundEntry.design_id);
            return Ok(new { message = "Tags removed successfully" });
        }
        [HttpDelete("{designId}/add-ons/{design-add-on-id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeleteDesignAddOn([FromRoute] string designId, [FromRoute] int design_add_on_id)
        {
            string decodedId = designId;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Invalid format in the designId value in route" }); }

            DesignAddOns? selectedAddOnForDesign = null;
            try { selectedAddOnForDesign = await _kaizenTables.DesignAddOns.Where(x => x.isActive == true && x.design_add_on_id == design_add_on_id && x.design_id.SequenceEqual(byteArrEncodedId)).FirstAsync(); }
            catch { return NotFound(new { message = "Design add on with the id " + design_add_on_id + " not found" }); }

            _kaizenTables.DesignAddOns.Update(selectedAddOnForDesign);
            selectedAddOnForDesign.isActive = false;
            selectedAddOnForDesign.last_modified_date = DateTime.Now;
            await _kaizenTables.SaveChangesAsync();

            return Ok(new { message = "Design add on for " + decodedId + " id " + selectedAddOnForDesign.design_add_on_id + " deleted" });
        }

        [HttpDelete("{designId}/tags/{tag-id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> RemoveDesignTagById([FromRoute] string designId, [FromRoute] Guid tag_id)
        {
            string decodedId = designId;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(designId);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Invalid format in the designId value in route" }); }

            Designs? foundEntry = null;
            try { foundEntry = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (InvalidOperationException e) { return NotFound(new { message = "Design with the specified id not found" }); }

            List<DesignTagsForCakes> currentTags = await _databaseContext.DesignTagsForCakes.Where(x => x.isActive == true && x.design_id == foundEntry.design_id).ToListAsync();

            DesignTagsForCakes? currentDesignTag = null;
            try
            {
                currentDesignTag = currentTags.Where(x => x.design_tag_id == tag_id).First();
                currentDesignTag.isActive = false;
                _databaseContext.DesignTagsForCakes.Update(currentDesignTag);
                await _databaseContext.SaveChangesAsync();
                return Ok(new { message = "Tag deleted" });

            }
            catch (Exception e) { return NotFound(new { message = "Tag does not exist in the design" }); }

        }

        private async Task<GetPastryMaterial> CreatePastryMaterialResponseFromDBRow(PastryMaterials data)
        {
            GetPastryMaterial response = new GetPastryMaterial();
            response.design_id = Convert.ToBase64String(data.design_id);
            try { Designs? selectedDesign = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id.SequenceEqual(data.design_id)).Select(x => new Designs { display_name = x.display_name }).FirstAsync(); response.design_name = selectedDesign.display_name; }
            catch (Exception e) { response.design_name = "N/A"; }

            response.pastry_material_id = data.pastry_material_id;
            response.date_added = data.date_added;
            response.last_modified_date = data.last_modified_date;
            response.main_variant_name = data.main_variant_name;
            response.ingredients_in_stock = true;

            List<GetPastryMaterialIngredients> responsePastryMaterialList = new List<GetPastryMaterialIngredients>();
            List<GetPastryMaterialAddOns> responsePastryMaterialAddOns = new List<GetPastryMaterialAddOns>();
            List<GetPastryMaterialSubVariant> responsePastryMaterialSubVariants = new List<GetPastryMaterialSubVariant>();
            double calculatedCost = 0.0;

            List<Ingredients> currentPastryMaterialIngredients = await _databaseContext.Ingredients.Where(x => x.isActive == true && x.pastry_material_id == data.pastry_material_id).ToListAsync();
            List<PastyMaterialAddOns> currentPastryMaterialAddOns = await _databaseContext.PastyMaterialAddOns.Where(x => x.isActive == true && x.pastry_material_id == data.pastry_material_id).ToListAsync();

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
                            Materials? currentReferencedMaterial = await _databaseContext.Materials.Where(x => x.material_id == currentIngredient.item_id && x.isActive == true).FirstAsync();
                            if (currentReferencedMaterial == null) { continue; }

                            newSubIngredientListEntry.item_name = currentReferencedMaterial.material_name;
                            newSubIngredientListEntry.item_id = currentReferencedMaterial.material_id;

                            List<MaterialIngredients> currentMaterialReferencedIngredients = await _databaseContext.MaterialIngredients.Where(x => x.material_id == currentIngredient.item_id).ToListAsync();

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
                                            Materials? currentSubMaterialReferencedMaterial = await _databaseContext.Materials.Where(x => x.material_id == materialIngredients.item_id && x.isActive == true).FirstAsync();
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

                                Materials currentReferencedMaterialForSub = await _databaseContext.Materials.Where(x => x.isActive == true && x.material_id == currentSubMaterial.item_id).FirstAsync();

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

                                List<MaterialIngredients> subMaterialIngredients = await _databaseContext.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentReferencedMaterialForSub.material_id).ToListAsync();
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
            foreach (PastyMaterialAddOns currentAddOn in currentPastryMaterialAddOns)
            {
                AddOns? referencedAddOns = null;
                try { referencedAddOns = await _kaizenTables.AddOns.Where(x => x.isActive == true && x.add_ons_id == currentAddOn.add_ons_id).FirstAsync(); }
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

            List<PastryMaterialSubVariants> currentPastryMaterialSubVariants = await _databaseContext.PastryMaterialSubVariants.Where(x => x.isActive == true && x.pastry_material_id == data.pastry_material_id).ToListAsync();
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

                List<PastryMaterialSubVariantIngredients> currentSubVariantIngredients = await _databaseContext.PastryMaterialSubVariantIngredients.Where(x => x.isActive == true && x.pastry_material_sub_variant_id == currentSubVariant.pastry_material_sub_variant_id).ToListAsync();
                List<PastryMaterialSubVariantAddOns> currentSubVariantAddOns = await _databaseContext.PastryMaterialSubVariantAddOns.Where(x => x.isActive == true && x.pastry_material_sub_variant_id == currentSubVariant.pastry_material_sub_variant_id).ToListAsync();

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
                                Materials? currentReferencedMaterial = await _databaseContext.Materials.Where(x => x.material_id == currentSubVariantIngredient.item_id && x.isActive == true).FirstAsync();
                                if (currentReferencedMaterial == null) { continue; }

                                newSubVariantIngredientListEntry.item_name = currentReferencedMaterial.material_name;
                                newSubVariantIngredientListEntry.item_id = currentReferencedMaterial.material_id;

                                List<MaterialIngredients> currentMaterialReferencedIngredients = await _databaseContext.MaterialIngredients.Where(x => x.material_id == currentSubVariantIngredient.item_id).ToListAsync();

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
                                                Materials? currentSubMaterialReferencedMaterial = await _databaseContext.Materials.Where(x => x.material_id == materialIngredients.item_id && x.isActive == true).FirstAsync();
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

                                    Materials currentReferencedMaterialForSub = await _databaseContext.Materials.Where(x => x.isActive == true && x.material_id == currentSubMaterial.item_id).FirstAsync();

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

                                    List<MaterialIngredients> subMaterialIngredients = await _databaseContext.MaterialIngredients.Where(x => x.isActive == true && x.material_id == currentReferencedMaterialForSub.material_id).ToListAsync();
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
                foreach (PastryMaterialSubVariantAddOns currentSubVariantAddOn in currentSubVariantAddOns)
                {
                    AddOns? referencedAddOns = null;
                    try { referencedAddOns = await _kaizenTables.AddOns.Where(x => x.isActive == true && x.add_ons_id == currentSubVariantAddOn.add_ons_id).FirstAsync(); }
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
            response.add_ons = responsePastryMaterialAddOns;
            response.sub_variants = responsePastryMaterialSubVariants;
            response.cost_estimate = calculatedCost;

            return response;
        }


    }
}
