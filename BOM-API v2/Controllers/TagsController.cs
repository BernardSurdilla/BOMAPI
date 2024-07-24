using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Helpers;
using BOM_API_v2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace BOM_API_v2.Controllers
{
    [Route("tags/")]
    [ApiController]
    public class TagsController : ControllerBase
    {
        private readonly DatabaseContext _databaseContext;
        private readonly KaizenTables _kaizenTables;
        private readonly IActionLogger _actionLogger;


        public TagsController(DatabaseContext databaseContext, IActionLogger actionLogger, KaizenTables kaizenTables) { _databaseContext = databaseContext; _actionLogger = actionLogger; _kaizenTables = kaizenTables; }

        [HttpGet]
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
        [HttpGet("{design_tag_id}")]
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

        [HttpPost]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewTags(PostTags input)
        {
            if (input == null) { return BadRequest(new { mesage = "Invalid input" }); }

            DesignTags newTags = new DesignTags();
            newTags.design_tag_id = new Guid();
            newTags.design_tag_name = input.design_tag_name;
            newTags.isActive = true;

            await _databaseContext.DesignTags.AddAsync(newTags);
            await _databaseContext.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add new tag " + newTags.design_tag_name.ToString());
            return Ok(new { message = "Tag " + newTags.design_tag_id + " created" });
        }

        [HttpPatch("{design_tag_id}")]
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

        [HttpDelete("{design_tag_id}")]
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
    }
}
