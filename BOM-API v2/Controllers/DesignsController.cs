using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Helpers;

using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BOM_API_v2.Services;

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
            IQueryable<Designs> dbQuery = _databaseContext.Designs.Where(x => x.is_active == true);

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
                GetDesign newResponseEntry = await DataParser.CreateGetDesignResponseFromDbRow(currentDesign, _databaseContext, _kaizenTables);

                response.Add(newResponseEntry);
            }

            await _actionLogger.LogAction(User, "GET", "All Design");
            return response;
        }
        [HttpGet("{design_id}")]
        public async Task<GetDesign> GetSpecificDesign([FromRoute]string design_id)
        {
            Designs? selectedDesign;
            string decodedId = design_id;
            byte[]? byteArrEncodedId = null;
            try
            {
                decodedId = Uri.UnescapeDataString(design_id);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return new GetDesign(); }

            try { selectedDesign = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id.SequenceEqual(byteArrEncodedId)).FirstAsync(); }
            catch (Exception e) { return new GetDesign(); }

            GetDesign response = await DataParser.CreateGetDesignResponseFromDbRow(selectedDesign, _databaseContext, _kaizenTables);
            /*
            List<DesignTagsForCakes> tagsForCurrentCake = await _databaseContext.DesignTagsForCakes.Include(x => x.DesignTags).Where(x => x.is_active == true && x.design_id.SequenceEqual(selectedDesign.design_id)).ToListAsync();

            DesignImage? currentDesignImage = null;
            try { currentDesignImage = await _databaseContext.DesignImage.Where(x => x.is_active == true && x.design_id == selectedDesign.design_id).FirstAsync(); }
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

            List<List<string>> design_idWithTags = new List<List<string>>();
            foreach (string design_id in design_ids)
            {
                DesignTags? currentTag;
                try { currentTag = await _databaseContext.DesignTags.Where(x => x.is_active == true && x.design_tag_id.ToString() == design_id).FirstAsync(); }
                catch (Exception e) { return new List<GetDesign>(); }

                List<DesignTagsForCakes> tagsForCakes = await _databaseContext.DesignTagsForCakes.Where(x => x.is_active == true && x.design_tag_id == currentTag.design_tag_id).ToListAsync();
                List<string> design_idsWithCurrentTag = new List<string>();
                foreach (DesignTagsForCakes designTagsForCakes in tagsForCakes)
                {
                    design_idsWithCurrentTag.Add(Convert.ToBase64String(designTagsForCakes.design_id));
                }
                design_idWithTags.Add(design_idsWithCurrentTag);
            }

            List<string>? cakeIdList = null;
            foreach (List<string> currentIdList in design_idWithTags)
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
                try { selectedDesign = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id.SequenceEqual(encodedId)).FirstAsync(); }
                catch (Exception e) { continue; }
                    
                response.Add(await DataParser.CreateGetDesignResponseFromDbRow(selectedDesign, _databaseContext, _kaizenTables));
            }

            await _actionLogger.LogAction(User, "GET", "All Design with specific tags ");
            return response;

        }
        [HttpGet("search/by-name")]
        public async Task<List<GetDesign>> SearchDesignByName(string name, int? page, int? record_per_page, string? sortBy, string? sortOrder)
        {

            IQueryable<Designs> dbQuery = _databaseContext.Designs.Where(x => x.is_active == true && EF.Functions.Like(x.display_name, $"%{name}%"));

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

            foreach (Designs currentDesign in current_design_records)
            {
                GetDesign newResponseEntry = await DataParser.CreateGetDesignResponseFromDbRow(currentDesign, _databaseContext, _kaizenTables);

                response.Add(newResponseEntry);
            }

            await _actionLogger.LogAction(User, "GET", "Search Design");
            return response;

        }

        [HttpGet("{design_id}/pastry-material")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<GetPastryMaterial> GetSpecificPastryMaterialByDesignId([FromRoute] string design_id)
        {
            Designs? selectedDesign;
            string decodedId = design_id;
            byte[]? byteArrEncodedId = null;
            try
            {
                decodedId = Uri.UnescapeDataString(design_id);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return new GetPastryMaterial(); }
            try { selectedDesign = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id.SequenceEqual(byteArrEncodedId)).FirstAsync(); }
            catch (Exception e) { return new GetPastryMaterial(); }

            PastryMaterials? currentPastryMat = null;
            try { currentPastryMat = await _databaseContext.PastryMaterials.Where(x => x.is_active == true && x.design_id.SequenceEqual(selectedDesign.design_id)).FirstAsync(); }
            catch (Exception e) { return new GetPastryMaterial(); }


            List<Ingredients> ingredientsForCurrentMaterial = await _databaseContext.Ingredients.Where(x => x.is_active == true && x.pastry_material_id == currentPastryMat.pastry_material_id).ToListAsync();
            Dictionary<string, List<string>> validMeasurementUnits = ValidUnits.ValidMeasurementUnits(); //List all valid units of measurement for the ingredients

            GetPastryMaterial response = await DataParser.CreatePastryMaterialResponseFromDBRow(currentPastryMat, _databaseContext, _kaizenTables);

            await _actionLogger.LogAction(User, "GET", "Pastry Material " + currentPastryMat.pastry_material_id);
            return response;

        }
        [HttpGet("without-pastry-material")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<List<GetDesignWithoutPastryMaterial>> GetDesignsWithoutPastryMaterial()
        {
            List<GetDesignWithoutPastryMaterial> response = new List<GetDesignWithoutPastryMaterial>();

            List<Designs> dbResp = await _databaseContext.Designs.Where(x => x.is_active == true && _databaseContext.PastryMaterials.Where(x=> x.is_active == true).Select(x => x.design_id).Contains(x.design_id) == false).Select(x => new Designs { design_id = x.design_id, display_name = x.display_name}).ToListAsync();
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

            //var dbResp = await _databaseContext.Designs.Where(x => x.is_active == true).LeftJoin()

            return response;
            /*

            List<Designs> dbResp = await _databaseContext.Designs.Where(x => x.is_active == true && _databaseContext.PastryMaterials.Where(x => x.is_active == true).Select(x => x.design_id).Contains(x.design_id) == true).Select(x => new Designs { design_id = x.design_id, display_name = x.display_name }).ToListAsync();
            foreach (Designs design in dbResp)
            {
                GetDesignWithoutPastryMaterial newResponseRow = new GetDesignWithoutPastryMaterial();
                newResponseRow.display_name = design.display_name;
                newResponseRow.design_id = design.design_id;
                response.Add(newResponseRow);
            }

        }
        */
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

            newEntry.is_active = true;

            //Remove dupes
            input.design_tag_ids = input.design_tag_ids.Distinct().ToList();

            List<DesignTagsForCakes> newTagRelationships = new List<DesignTagsForCakes>();
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
                        newTagReference.is_active = true;

                        newTagRelationships.Add(newTagReference);
                    }
                }
            }

            List<DesignShapes> newDesignShapes = new List<DesignShapes>();
            if (input.design_shape_names != null)
            {
                foreach (string shapeName in input.design_shape_names)
                {
                    newDesignShapes.Add(new DesignShapes
                    {
                        design_shape_id = Guid.NewGuid(),
                        shape_name = shapeName,
                        design_id = newEntryId,
                        is_active = true
                    });
                }
            }

            DesignImage? newDesignImage = null;
            if (input.display_picture_data != null)
            {
                newDesignImage = new DesignImage();
                newDesignImage.design_id = newEntry.design_id;
                newDesignImage.design_picture_id = new Guid();
                newDesignImage.picture_data = input.display_picture_data;
                newDesignImage.is_active = true;
            }
            
            _databaseContext.Designs.Add(newEntry);
            _databaseContext.SaveChanges();

            if (newTagRelationships.IsNullOrEmpty() == false) 
            { 
                await _databaseContext.DesignTagsForCakes.AddRangeAsync(newTagRelationships); 
                await _databaseContext.SaveChangesAsync(); 
            }
            if (newDesignShapes.IsNullOrEmpty() == false)
            {
                await _databaseContext.DesignShapes.AddRangeAsync(newDesignShapes);
                await _databaseContext.SaveChangesAsync();
            }
            if (newDesignImage != null) 
            { 
                await _databaseContext.DesignImage.AddAsync(newDesignImage); 
                await _databaseContext.SaveChangesAsync(); 
            }

            await _actionLogger.LogAction(User, "POST", "Add new design " + newEntryId.ToString());
            return Ok(new { message = "Design " + Convert.ToBase64String(newEntryId) + " added", id = Convert.ToBase64String(newEntryId) });
        }
        [HttpPost("{design_id}/shapes/")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddNewShape(string shape_name, [FromRoute] string design_id)
        {
            Designs? selectedDesign;
            string decodedId = design_id;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(design_id);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Cannot convert design id on route to string" }); }

            try { selectedDesign = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (Exception e) { return NotFound(new { message = "Design id not found" }); }

            DesignShapes? sameShapeName = await _databaseContext.DesignShapes.Where(x => x.is_active == true && x.shape_name == shape_name).FirstOrDefaultAsync();
            if (sameShapeName != null) { return BadRequest(new { message = "Shape with the same name already exists" }); }

            DesignShapes newShapeAssociation = new DesignShapes
            {
                design_shape_id = Guid.NewGuid(),
                design_id = selectedDesign.design_id,
                shape_name = shape_name,
                is_active = true
            };
            await _databaseContext.DesignShapes.AddAsync(newShapeAssociation);
            await _databaseContext.SaveChangesAsync();

            return Ok(new { message = "New shape (" + shape_name + ") associated with design " + design_id });
        }
        
        [HttpPut("{design_id}/tags")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AddDesignTags(PostDesignTags input, [FromRoute] string design_id)
        {
            Designs? selectedDesign;
            string decodedId = design_id;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(design_id);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Cannot convert design id on route to string" }); }

            try { selectedDesign = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (Exception e) { return NotFound(new { message = "Design id not found" }); }

            if (input == null) { return BadRequest(new { message = "Input is null" }); }
            if (input.design_tag_ids.IsNullOrEmpty()) { return BadRequest(new { message = "No tag ids in the input body" }); }

            foreach (Guid tagId in input.design_tag_ids)
            {
                DesignTags referencedTag;
                try { referencedTag = await _databaseContext.DesignTags.Where(x => x.is_active == true && x.design_tag_id == tagId).FirstAsync(); }
                catch (InvalidOperationException e) { return BadRequest(new { message = "The tag with the id " + tagId + " does not exist" }); }
                catch (Exception e) { return StatusCode(500, new { message = e.GetType().ToString() }); }

                DesignTagsForCakes? currentDesignTagConnection = null;
                try
                {
                    currentDesignTagConnection = await _databaseContext.DesignTagsForCakes.Where(x => x.design_id.SequenceEqual(byteArrEncodedId) == true && x.design_tag_id == referencedTag.design_tag_id).FirstAsync();

                    if (currentDesignTagConnection.is_active == false)
                    {
                        _databaseContext.DesignTagsForCakes.Update(currentDesignTagConnection);
                        currentDesignTagConnection.is_active = true;
                    }
                    else { continue; }
                }
                catch
                {
                    DesignTagsForCakes newDesignTag = new DesignTagsForCakes();
                    newDesignTag.is_active = true;
                    newDesignTag.design_tags_for_cake_id = new Guid();
                    newDesignTag.design_id = selectedDesign.design_id;
                    newDesignTag.design_tag_id = referencedTag.design_tag_id;
                    _databaseContext.DesignTagsForCakes.Add(newDesignTag);
                }
            }
            await _databaseContext.SaveChangesAsync();
            return Ok(new { message = "Tags inserted to " + selectedDesign.design_id.ToString() });
        }

        [HttpPatch("{design_id}/")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdateDesign(PostDesign input, [FromRoute]string design_id)
        {
            string decodedId = design_id;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(design_id);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new {message="Invalid format in the design_id value in route"}); }

            Designs? foundEntry = null;
            try { foundEntry = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (InvalidOperationException e) { return NotFound(new { message = "Design with the specified id not found" }); }
            catch (Exception e) { return BadRequest(new { message = "An unspecified error occured when retrieving the data" }); }

            _databaseContext.Designs.Update(foundEntry);
            foundEntry.display_name = input.display_name;
            foundEntry.cake_description = input.cake_description;
            foundEntry.display_picture_url = input.display_picture_url;

            List<DesignTagsForCakes> allDesignTagsForCakes = await _databaseContext.DesignTagsForCakes.Where(x => x.is_active == true && x.design_id == foundEntry.design_id).ToListAsync();

            List<Guid> normalizedInputTagIdList = input.design_tag_ids != null ? input.design_tag_ids.Distinct().ToList() : new List<Guid>();

            foreach (Guid currentTagId in normalizedInputTagIdList)
            {
                DesignTags referencedTag;
                try { referencedTag = await _databaseContext.DesignTags.Where(x => x.is_active == true && x.design_tag_id == currentTagId).FirstAsync(); }
                catch (InvalidOperationException e) { return BadRequest(new { message = "The tag with the id " + currentTagId + " does not exist" }); }
                catch (Exception e) { return StatusCode(500, new { message = e.GetType().ToString() }); }

                if (allDesignTagsForCakes.Where(x => x.design_tag_id == currentTagId).IsNullOrEmpty())
                {
                    DesignTagsForCakes newTagConnection = new DesignTagsForCakes();
                    newTagConnection.design_tags_for_cake_id = new Guid();
                    newTagConnection.design_id = byteArrEncodedId;
                    newTagConnection.design_tag_id = currentTagId;
                    newTagConnection.is_active = true;

                    await _databaseContext.DesignTagsForCakes.AddAsync(newTagConnection);
                }
            }
            List<DesignShapes> allShapesForDesign = await _databaseContext.DesignShapes.Where(x => x.is_active == true && x.design_id == foundEntry.design_id).ToListAsync();
            List<string> normalizedShapeNames = input.design_shape_names != null ? input.design_shape_names.Distinct().ToList() : new List<string>();
            foreach (string shapeName in normalizedShapeNames)
            {
                DesignShapes? duplicateCheck = allShapesForDesign.Where(x => x.shape_name == shapeName).FirstOrDefault();

                if (duplicateCheck != null) continue;
                DesignShapes newShapeAssociation = new DesignShapes
                {
                    design_id = foundEntry.design_id,
                    design_shape_id = Guid.NewGuid(),
                    shape_name = shapeName,
                    is_active = true
                };
                await _databaseContext.DesignShapes.AddAsync(newShapeAssociation);
            }


            if (input.display_picture_data != null)
            {
                DesignImage? designImage = null;
                try { designImage = await _databaseContext.DesignImage.Where(x => x.is_active == true && x.design_id.SequenceEqual(byteArrEncodedId)).FirstAsync(); }
                catch { }
                if (designImage == null)
                {
                    designImage = new DesignImage();
                    designImage.design_id = byteArrEncodedId;
                    designImage.design_picture_id = new Guid();
                    designImage.picture_data = input.display_picture_data;
                    designImage.is_active = true;
                    await _databaseContext.DesignImage.AddAsync(designImage);
                }
                else
                {
                    designImage.picture_data = input.display_picture_data;
                    _databaseContext.DesignImage.Update(designImage);
                }
            }
            await _databaseContext.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update design " + decodedId);
            return Ok(new { message = "Design " + design_id.ToString() + " updated" });
        }
        [HttpPatch("{design_id}/shapes/{design_shape_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> UpdateDesignShapeName(string design_id, Guid design_shape_id, [FromBody]string design_shape_name)
        {
            Designs? selectedDesign;
            string decodedId = design_id;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(design_id);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Cannot convert design id on route to string" }); }

            try { selectedDesign = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (Exception e) { return NotFound(new { message = "Design id not found" }); }

            DesignShapes? selectedShape = await _databaseContext.DesignShapes.Where(x => x.is_active == true && x.design_id.SequenceEqual(selectedDesign.design_id) && x.design_shape_id == design_shape_id).FirstOrDefaultAsync();
            if (selectedShape == null) { return NotFound(new {message = "Shape with the id " + design_shape_id + " does not exist or deleted"}); }

            _databaseContext.DesignShapes.Update(selectedShape);
            selectedShape.shape_name = design_shape_name;
            await _databaseContext.SaveChangesAsync();

            return Ok(new { message = "Shape " + design_shape_id + " updated" });

        }


        [HttpDelete("{design_id}/")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> DeleteDesign([FromRoute] string design_id)
        {
            string decodedId = design_id;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(design_id);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Invalid format in the design_id value in route" }); }

            Designs? foundEntry = null;
            try { foundEntry = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (InvalidOperationException e) { return NotFound(new { message = "Design with the specified id not found" }); }
            catch (Exception e) { return BadRequest(new { message = "An unspecified error occured when retrieving the data" }); }

            _databaseContext.Designs.Update(foundEntry);
            foundEntry.is_active = false;
            await _databaseContext.SaveChangesAsync();
            

            await _actionLogger.LogAction(User, "DELETE", "Delete design " + decodedId);
            return Ok(new { message = "Design " + decodedId + " deleted" });
        }
        [HttpDelete("{design_id}/tags")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> RemoveDesignTag([FromRoute]string design_id, [FromBody] List<Guid> tag_ids)
        {
            string decodedId = design_id;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(design_id);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Invalid format in the design_id value in route" }); }

            Designs? foundEntry = null;
            try { foundEntry = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (InvalidOperationException e) { return NotFound(new { message = "Design with the specified id not found" }); }

            List<DesignTagsForCakes> currentTags = await _databaseContext.DesignTagsForCakes.Where(x => x.is_active == true && x.design_id == foundEntry.design_id).ToListAsync();
            List<Guid> normalizedCakeTagIds = tag_ids.Distinct().ToList();
            foreach (Guid tagId in normalizedCakeTagIds)
            {
                DesignTagsForCakes? currentReferencedTag = currentTags.Where(x => x.design_tag_id == tagId).FirstOrDefault();
                if (currentReferencedTag != null)
                {
                    _databaseContext.DesignTagsForCakes.Update(currentReferencedTag);
                    currentReferencedTag.is_active = false;
                }
            }
            await _databaseContext.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE", "Tags for " + foundEntry.design_id);
            return Ok(new { message = "Tags removed successfully" });
        }
        [HttpDelete("{design_id}/shapes/{design_shape_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> RemoveDesignShape(string design_id, Guid design_shape_id)
        {

            Designs? selectedDesign;
            string decodedId = design_id;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(design_id);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Cannot convert design id on route to string" }); }

            try { selectedDesign = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (Exception e) { return NotFound(new { message = "Design id not found" }); }

            DesignShapes? selectedShape = await _databaseContext.DesignShapes.Where(x => x.is_active == true && x.design_id.SequenceEqual(selectedDesign.design_id) && x.design_shape_id == design_shape_id).FirstOrDefaultAsync();
            if (selectedShape == null) { return NotFound(new { message = "Shape with the id " + design_shape_id + " does not exist or deleted" }); }

            _databaseContext.DesignShapes.Update(selectedShape);
            selectedShape.is_active = false;
            await _databaseContext.SaveChangesAsync();

            return Ok(new { message = "Shape " + design_shape_id + " deleted" });

        }

        [HttpDelete("{design_id}/tags/{tag_id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> RemoveDesignTagById([FromRoute] string design_id, [FromRoute] Guid tag_id)
        {
            string decodedId = design_id;
            byte[]? byteArrEncodedId = null;

            try
            {
                decodedId = Uri.UnescapeDataString(design_id);
                byteArrEncodedId = Convert.FromBase64String(decodedId);
            }
            catch { return BadRequest(new { message = "Invalid format in the design_id value in route" }); }

            Designs? foundEntry = null;
            try { foundEntry = await _databaseContext.Designs.Where(x => x.is_active == true && x.design_id == byteArrEncodedId).FirstAsync(); }
            catch (InvalidOperationException e) { return NotFound(new { message = "Design with the specified id not found" }); }

            List<DesignTagsForCakes> currentTags = await _databaseContext.DesignTagsForCakes.Where(x => x.is_active == true && x.design_id == foundEntry.design_id).ToListAsync();

            DesignTagsForCakes? currentDesignTag = null;
            try
            {
                currentDesignTag = currentTags.Where(x => x.design_tag_id == tag_id).First();
                currentDesignTag.is_active = false;
                _databaseContext.DesignTagsForCakes.Update(currentDesignTag);
                await _databaseContext.SaveChangesAsync();
                return Ok(new { message = "Tag deleted" });

            }
            catch (Exception e) { return NotFound(new { message = "Tag does not exist in the design" }); }

        }

    }
}
