using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Helpers;

using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BillOfMaterialsAPI.Services;


namespace BOM_API_v2.Controllers
{
    [ApiController]
    [Authorize(Roles = UserRoles.Admin)]
    [Route("BOM/designs/")]
    public class DesignsController : ControllerBase
    {
        private readonly DatabaseContext _databaseContext;
        private readonly IActionLogger _actionLogger;

        public DesignsController(DatabaseContext databaseContext, IActionLogger actionLogger) { _databaseContext = databaseContext; _actionLogger = actionLogger; }

        [HttpGet]
        public async Task<List<Designs>> GetAllDesigns(int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {
            IQueryable<Designs> dbQuery = _databaseContext.Designs.Where(x => x.isActive == true);

            List<Designs> response = new List<Designs>();

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
            if (page == null) { response = await dbQuery.ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < Page.DefaultNumberOfEntriesPerPage ? Page.DefaultNumberOfEntriesPerPage : record_per_page.Value;
                int current_page = page.Value < Page.DefaultStartingPageNumber ? Page.DefaultStartingPageNumber : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                response = await dbQuery.Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            await _actionLogger.LogAction(User, "GET", "All Design");
            return response;
        }

        [HttpPost]
        public async Task<IActionResult> AddNewDesign(PostDesign input)
        {
            byte[] newEntryId = Guid.NewGuid().ToByteArray();

            Designs newEntry = new Designs();
            newEntry.design_id = newEntryId;
            newEntry.display_name = input.display_name;
            newEntry.display_picture_url = input.display_picture_url;
            newEntry.isActive = true;

            await _databaseContext.AddAsync(newEntry);
            await _databaseContext.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Add new design " + newEntryId.ToString());
            return Ok(new { message = "Design " + newEntryId.ToString() + " added" });
        }

        [HttpPatch("{designId}/")]
        public async Task<IActionResult> UpdateDesign(PostDesign input, [FromRoute]byte[] designId)
        {
            Designs? foundEntry = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id == designId).FirstAsync();

            if (foundEntry == null) { return NotFound(new { message = "Design with the specified id not found" }); }
            else
            {
                _databaseContext.Designs.Update(foundEntry);
                foundEntry.display_name = input.display_name;
                foundEntry.display_picture_url = input.display_picture_url;
                await _databaseContext.SaveChangesAsync();
            }

            await _actionLogger.LogAction(User, "PATCH", "Add new design " + designId.ToString());
            return Ok(new { message = "Design " + designId.ToString() + " updated" });
        }

        [HttpDelete("{designId}/")]
        public async Task<IActionResult> DeleteDesign([FromRoute] byte[] designId)
        {
            Designs? foundEntry = await _databaseContext.Designs.Where(x => x.isActive == true && x.design_id == designId).FirstAsync();

            if (foundEntry == null) { return NotFound(new { message = "Design with the specified id not found" }); }
            else
            {
                _databaseContext.Designs.Update(foundEntry);
                foundEntry.isActive = false;
                await _databaseContext.SaveChangesAsync();
            }

            await _actionLogger.LogAction(User, "DELETE", "Delete design " + designId.ToString());
            return Ok(new { message = "Design " + designId.ToString() + " deleted" });
        }

    }
}
