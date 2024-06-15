﻿using BillOfMaterialsAPI.Models;
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


namespace BOM_API_v2.Controllers
{
    [ApiController]
    [Authorize]
    [Route("BOM/designs/")]
    public class DesignsController : ControllerBase
    {
        private readonly DatabaseContext _databaseContext;
        private readonly IActionLogger _actionLogger;

        public DesignsController(DatabaseContext databaseContext, IActionLogger actionLogger) { _databaseContext = databaseContext; _actionLogger = actionLogger; }

        [HttpGet]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Customer)]
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
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Customer)]
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
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Customer)]
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

            try { selectedDesign = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id == byteArrEncodedId).FirstAsync(); }
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
        [HttpGet("tags/{design_tag_id}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Customer)]
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
        
        private async Task<GetDesign> CreateGetDesignResponseFromDbRow(Designs data)
        {
            GetDesign response = new GetDesign();

            response.design_id = data.design_id;
            response.display_name = data.display_name;
            response.design_picture_url = data.display_picture_url;
            response.cake_description = data.cake_description;
            response.design_tags = new List<GetDesignTag>();

            List<DesignTagsForCakes> cakeTags = await _databaseContext.DesignTagsForCakes.Include(x => x.DesignTags).Where(x => x.isActive == true && x.design_id == data.design_id && x.DesignTags.isActive == true).ToListAsync();
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
            if (image != null) { response.display_picture_data = image.picture_data; }
            else { response.display_picture_data = null; };

            return response;
        }

        [HttpPost]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewDesign(PostDesign input)
        {
            byte[] newEntryId = Guid.NewGuid().ToByteArray();

            Designs newEntry = new Designs();
            newEntry.design_id = newEntryId;
            newEntry.display_name = input.display_name;
            newEntry.display_picture_url = input.display_picture_url;
            newEntry.cake_description = input.cake_description;

            newEntry.isActive = true;

            List<DesignTagsForCakes> newTagRelationships = new List<DesignTagsForCakes>();
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
            
            if (newDesignImage != null) { await _databaseContext.DesignImage.AddAsync(newDesignImage); await _databaseContext.SaveChangesAsync(); }

            await _actionLogger.LogAction(User, "POST", "Add new design " + newEntryId.ToString());
            return Ok(new { message = "Design " + newEntryId.ToString() + " added" });
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
        [HttpPost("{designId}/tags")]
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
            catch (Exception e) { return NotFound(new {message = "Design id not found"}); }

            if (input == null) { return BadRequest(new { message = "Input is null" }); }
            if (input.design_tag_ids.IsNullOrEmpty()) { return BadRequest(new { message = "No tag ids in the input body" }); }

            foreach (Guid tagId in input.design_tag_ids)
            {
                DesignTags referencedTag;
                try { referencedTag = await _databaseContext.DesignTags.Where(x => x.isActive == true && x.design_tag_id == tagId).FirstAsync(); }
                catch (InvalidOperationException e) { return BadRequest(new { message = "The tag with the id " + tagId + " does not exist" }); }
                catch (Exception e) { return StatusCode(500, new { message = e.GetType().ToString() }); }

                DesignTagsForCakes newDesignTag = new DesignTagsForCakes();
                newDesignTag.isActive = true;
                newDesignTag.design_tags_for_cake_id = new Guid();
                newDesignTag.design_id = selectedDesign.design_id;
                newDesignTag.design_tag_id = referencedTag.design_tag_id;
                _databaseContext.DesignTagsForCakes.Add(newDesignTag);
            }
            await _databaseContext.SaveChangesAsync();
            return Ok(new {message = "Tags inserted to " + selectedDesign.design_id.ToString()});
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

            await _databaseContext.SaveChangesAsync();
            

            await _actionLogger.LogAction(User, "PATCH", "Update design " + decodedId);
            return Ok(new { message = "Design " + designId.ToString() + " updated" });
        }
        [HttpPatch("tags/{design_tag_id}")]
        public async Task<IActionResult> UpdateTag(PostTags input, [FromRoute] Guid design_tag_id)
        {
            DesignTags? selectedDesignTag;
            try { selectedDesignTag = await _databaseContext.DesignTags.Where(x => x.isActive == true && x.design_tag_id == design_tag_id).FirstAsync(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = "Specified design tag with the id " + design_tag_id + " does not exist" }); }
            catch (Exception e) { return BadRequest(new { message = "An unspecified error occured when retrieving the data" }); }

            _databaseContext.DesignTags.Update(selectedDesignTag);
            selectedDesignTag.design_tag_name = input.design_tag_name;
            await _databaseContext.SaveChangesAsync();

            _actionLogger.LogAction(User, "PATCH", "Update design tag " + selectedDesignTag.design_tag_id);
            return Ok(new { message = "Design " + selectedDesignTag.design_tag_id + " updated" });
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
        [HttpDelete("tags/{design_tag_id}")]
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
    }
}
