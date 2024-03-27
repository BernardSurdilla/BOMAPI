using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using BillOfMaterialsAPI.Services;
using JWTAuthentication.Authentication;
using System.Security.Claims;

namespace API_TEST.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public static class IdFormat
    {
        public static string materialIdFormat = "MID";
        public static string materialIngredientIdFormat = "MIID";
        public static string ingredientIdFormat = "IID";
        public static string logsIdFormat = "LOG";
        public static int idNumLength = 12;

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
    }

    [ApiController]
    [Route("BOM/")]
    [Authorize(Roles = UserRoles.Admin)]
    public class BOMController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly IActionLogger _actionLogger;


        public BOMController(DatabaseContext context, IActionLogger logger)
        {
            _context = context;
            _actionLogger = logger;
        }

        [HttpGet]
        public string yeeass()
        {
            return User.Claims.First(x => x.Type == ClaimTypes.Name).Value;
        }
        //Json serialization and deserialization
        //string a = JsonSerializer.Serialize(ingredientAboutToBeDeleted);
        //JsonSerializer.Deserialize<List<SubPastryMaterials_materials_column>>(a);
    }


    [ApiController]
    [Route("BOM/materials/")]
    [Authorize(Roles = UserRoles.Admin)]
    public class MaterialController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly IActionLogger _actionLogger;

        public MaterialController(DatabaseContext context, IActionLogger logs)
        {
            _context = context; _actionLogger = logs;
        }

        //GET
        [HttpGet]
        public async Task<List<GetMaterials>> GetAllMaterials()
        {
            
            //Default GET request without parameters
            //This should return all records in the BOM database

            //Container for response
            List<GetMaterials> response = new List<GetMaterials>();

            //Get all entries in the 'Materials' and 'MaterialIngredients' table in the connected database
            //And convert it to a list object
            List<Materials> dbMaterials = await _context.Materials.ToListAsync();
            List<MaterialIngredients> dbMaterialIngredients = await _context.MaterialIngredients.ToListAsync();
            //Get only the rows with the 'isActive' column true
            dbMaterials = dbMaterials.FindAll(x => x.isActive == true);
            dbMaterialIngredients = dbMaterialIngredients.FindAll(x => x.isActive == true);

            foreach (Materials material in dbMaterials)
            {
                string currentMaterialId = material.material_id;
                List<MaterialIngredients> currentMaterialIngredientsList = dbMaterialIngredients.FindAll(x => x.material_id == currentMaterialId);

                SubGetMaterials currentMaterial = new SubGetMaterials(material);
                List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();
                foreach (MaterialIngredients materialIngredients in currentMaterialIngredientsList)
                {
                    SubGetMaterialIngredients x = new SubGetMaterialIngredients(materialIngredients);
                    currentMaterialIngredients.Add(x);
                }


                int estimatedCost = 0;
                //Code to calculate cost here
                //
                //

                GetMaterials row = new GetMaterials(currentMaterial, currentMaterialIngredients, estimatedCost);
                response.Add(row);
            }

            await _actionLogger.LogAction(User, "GET, All materials");
            return response;
        }
        [HttpGet("{material_id}")]
        public async Task<GetMaterials?> GetMaterial(string material_id)
        {
            //GET request with the material_id specified
            //This should return the row with the specified material_id

            GetMaterials response;

            Materials? currentMaterial = await _context.Materials.FindAsync(material_id);

            if (currentMaterial == null) { return GetMaterials.DefaultResponse(); }
            if (currentMaterial.isActive == false) { return GetMaterials.DefaultResponse(); }

            List<MaterialIngredients> x = await _context.MaterialIngredients.ToListAsync();
            List<MaterialIngredients> y = x.FindAll(x => x.material_id == currentMaterial.material_id && x.isActive == true);

            List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();

            foreach (MaterialIngredients material in y)
            {
                SubGetMaterialIngredients z = new SubGetMaterialIngredients(material);
                currentMaterialIngredients.Add(z);
            }
            int estimatedCost = 0;
            //Code to calculate cost here
            //
            //
            response = new GetMaterials(new SubGetMaterials(currentMaterial), currentMaterialIngredients, estimatedCost);

            await _actionLogger.LogAction(User, "GET, Material " + currentMaterial.material_id);
            return response;

            /*
            if (currentMaterial != null)
            {
                if (currentMaterial.isActive == true)
                {
                    List<MaterialIngredients> x = await _context.MaterialIngredients.ToListAsync();
                    List<MaterialIngredients> y = x.FindAll(x => x.material_id == currentMaterial.material_id && x.isActive == true);

                    List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();

                    foreach (MaterialIngredients material in y)
                    {
                        SubGetMaterialIngredients z = new SubGetMaterialIngredients(material);
                        currentMaterialIngredients.Add(z);
                    }
                    int estimatedCost = 0;
                    //Code to calculate cost here
                    //
                    //
                    response = new GetMaterials(new SubGetMaterials(currentMaterial), currentMaterialIngredients, estimatedCost);

                    return response;
                }
                else { return GetMaterials.DefaultResponse(); }
            }
            else { return GetMaterials.DefaultResponse(); }
            */
        }
        [HttpGet("{material_id}/{column_name}")]
        public async Task<object> GetMaterialColumn(string material_id, string column_name)
        {
            Materials? currentMaterial = await _context.Materials.FindAsync(material_id);

            Object? response = null;

            if (currentMaterial == null) { response = NotFound(new { message = "Specified material with the material_id is not found or deleted." }); }
            if (currentMaterial.isActive == false) { response = NotFound(new { message = "Specified material with the material_id is not found or deleted." }); }

            switch (column_name)
            {
                case "material_id": response = currentMaterial.material_id; break;
                case "material_name": response = currentMaterial.material_name; break;
                case "amount": response = currentMaterial.amount; break;
                case "amount_measurement": response = currentMaterial.amount_measurement; break;
                case "dateAdded": response = currentMaterial.dateAdded; break;
                case "lastModifiedDate": response = currentMaterial.lastModifiedDate; break;
                case "cost_estimate":
                    //Replace this with code that calculates the price estimation
                    response = 0;
                    break;
                case "material_ingredients":
                    List<MaterialIngredients> x = _context.MaterialIngredients.ToList();
                    List<MaterialIngredients> y = x.FindAll(x => x.material_id == currentMaterial.material_id && x.isActive == true);

                    List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();
                    foreach (MaterialIngredients material in y) { currentMaterialIngredients.Add(new SubGetMaterialIngredients(material)); }
                    response = currentMaterialIngredients;
                    break;

                default: response = BadRequest(new { message = "Specified column does not exist." }); break;
            }

            await _actionLogger.LogAction(User, "GET, Material " + material_id + " - Column " + column_name);
            return response;
            /*
            if (currentMaterial != null)
            {
                if (currentMaterial.isActive == true)
                {
                    switch (column_name)
                    {
                        case "material_id": response = currentMaterial.material_id; break;
                        case "material_name": response = currentMaterial.material_name; break;
                        case "amount": response = currentMaterial.amount; break;
                        case "amount_measurement": response = currentMaterial.amount_measurement; break;
                        case "dateAdded": response = currentMaterial.dateAdded; break;
                        case "lastModifiedDate": response = currentMaterial.lastModifiedDate; break;
                        case "cost_estimate":
                            //Replace this with code that calculates the price estimation
                            response = 0;
                            break;
                        case "material_ingredients":
                            List<MaterialIngredients> x = _context.MaterialIngredients.ToList();
                            List<MaterialIngredients> y = x.FindAll(x => x.material_id == currentMaterial.material_id && x.isActive == true);

                            List<SubGetMaterialIngredients> currentMaterialIngredients = new List<SubGetMaterialIngredients>();
                            foreach (MaterialIngredients material in y) { currentMaterialIngredients.Add(new SubGetMaterialIngredients(material)); }
                            response = currentMaterialIngredients;
                            break;

                        default: response = BadRequest(new { message = "Specified column does not exist." }); break;
                    }
                }
            }
            else { response = NotFound(new { message = "Specified material with the material_id is not found or deleted." }); }

            return response;
            */
        }

        //POST
        [HttpPost]
        public async Task<IActionResult> AddMaterialAndMaterialIngredients(PostMaterial_MaterialIngredients data)
        {

            if (data.ingredients.IsNullOrEmpty()) { return BadRequest(new { message = "Ingredients to be inserted is empty or null." }); }

            //Check if the 'item_id' in the 'SubPostMaterialIngredients' list inside of the data from the client 
            //exists in the inventory
            foreach (SubPostMaterialIngredients ing in data.ingredients)
            {
                //Code for checking the id here
            }
            try
            {
                Materials newMaterialsEntry = new Materials();
                List<MaterialIngredients> newIngredientsEntry = new List<MaterialIngredients>();

                DateTime currentTime = DateTime.Now;

                string lastMaterialId = "";
                string lastMaterialIngredientId = "";

                try { List<Materials> x = await _context.Materials.ToListAsync(); lastMaterialId = x.Last().material_id; }
                catch (Exception ex)
                {
                    string newMaterialId = IdFormat.materialIdFormat;
                    for (int i = 1; i <= IdFormat.idNumLength; i++) { newMaterialId += "0"; }
                    lastMaterialId = newMaterialId;
                }
                try { List<MaterialIngredients> x = await _context.MaterialIngredients.ToListAsync(); lastMaterialIngredientId = x.Last().material_ingredient_id; }
                catch (Exception ex)
                {
                    string newIngredientId = IdFormat.materialIngredientIdFormat;
                    for (int i = 1; i <= IdFormat.idNumLength; i++) { newIngredientId += "0"; }
                    lastMaterialIngredientId = newIngredientId;
                }

                newMaterialsEntry.material_id = IdFormat.IncrementId(IdFormat.materialIdFormat, IdFormat.idNumLength, lastMaterialId);
                newMaterialsEntry.material_name = data.material_name;
                newMaterialsEntry.amount = data.amount;
                newMaterialsEntry.amount_measurement = data.amount_measurement;

                newMaterialsEntry.isActive = true;
                newMaterialsEntry.dateAdded = currentTime;
                newMaterialsEntry.lastModifiedDate = currentTime;

                foreach (SubPostMaterialIngredients i in data.ingredients)
                {
                    MaterialIngredients currentMatIng = new MaterialIngredients();
                    string newMaterialIngredientId = IdFormat.IncrementId(IdFormat.materialIngredientIdFormat, IdFormat.idNumLength, lastMaterialIngredientId);

                    currentMatIng.material_id = newMaterialsEntry.material_id;
                    currentMatIng.item_id = i.item_id;
                    currentMatIng.material_ingredient_id = newMaterialIngredientId;
                    currentMatIng.amount = i.amount;
                    currentMatIng.amount_measurement = i.amount_measurement;

                    currentMatIng.isActive = true;
                    currentMatIng.dateAdded = currentTime;
                    currentMatIng.lastModifiedDate = currentTime;

                    lastMaterialIngredientId = newMaterialIngredientId;

                    newIngredientsEntry.Add(currentMatIng);
                }

                await _context.Materials.AddAsync(newMaterialsEntry);
                _context.SaveChanges();

                await _context.MaterialIngredients.AddRangeAsync(newIngredientsEntry);
                _context.SaveChanges();

                await _actionLogger.LogAction(User, "POST, Add Material " + newMaterialsEntry.material_id);
                return Ok(new { message = "Data inserted to the database." });

            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error on creating new material.");
            }
        }
        [HttpPost("{material_id}/ingredients")]
        public async Task<IActionResult> AddMaterialIngredients(string material_id, List<SubPostMaterialIngredients> materialIngredients)
        {
            if (await _context.Materials.FindAsync(material_id) == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialIngredients == null || materialIngredients.IsNullOrEmpty()) { return BadRequest(new { message = "Material ingredients to be inserted is empty or null." }); }

            //Check if the 'item_id' in the 'SubPostMaterialIngredients' list inside of the data from the client 
            //exists in the inventory
            foreach (SubPostMaterialIngredients ing in materialIngredients)
            {
                //Code for checking the id here
            }

            List<MaterialIngredients> newMaterialIngredientsEntry = new List<MaterialIngredients>();

            string lastMaterialIngredientId = "";
            try { List<MaterialIngredients> x = await _context.MaterialIngredients.ToListAsync(); lastMaterialIngredientId = x.Last().material_ingredient_id; }
            catch (Exception ex)
            {
                string newIngredientId = IdFormat.materialIngredientIdFormat;
                for (int i = 1; i <= IdFormat.idNumLength; i++) { newIngredientId += "0"; }
                lastMaterialIngredientId = newIngredientId;
            }

            foreach (SubPostMaterialIngredients i in materialIngredients)
            {
                MaterialIngredients currentMatIng = new MaterialIngredients();
                string newMaterialIngredientId = IdFormat.IncrementId(IdFormat.materialIngredientIdFormat, IdFormat.idNumLength, lastMaterialIngredientId);

                DateTime currentTime = DateTime.Now;

                currentMatIng.material_id = material_id;
                currentMatIng.item_id = i.item_id;
                currentMatIng.material_ingredient_id = newMaterialIngredientId;
                currentMatIng.amount = i.amount;
                currentMatIng.amount_measurement = i.amount_measurement;
                currentMatIng.isActive = true;
                currentMatIng.dateAdded = currentTime;
                currentMatIng.lastModifiedDate = currentTime;

                lastMaterialIngredientId = newMaterialIngredientId;

                newMaterialIngredientsEntry.Add(currentMatIng);
            }

            try
            {
                await _context.MaterialIngredients.AddRangeAsync(newMaterialIngredientsEntry);
                _context.SaveChanges();
            }
            catch (Exception ex) { return Problem("Data not inserted to database."); }

            await _actionLogger.LogAction(User, "POST, Add Material Ingredient to " + material_id);
            return Ok( new {message = "Material ingredient inserted to database."});
        }

        //PATCH
        [HttpPatch("{material_id}")]
        public async Task<IActionResult> UpdateMaterials(string material_id, PatchMaterials entry)
        {
            Materials? materialAboutToBeUpdated = await _context.Materials.FindAsync(material_id);
            if (materialAboutToBeUpdated == null) { return NotFound(new { message = "Specified material entry with the selected material_id not found." }); }
            if (materialAboutToBeUpdated.isActive == false) { return NotFound(new { message = "Specified material entry with the selected material_id not found or deleted." }); }

            DateTime currentTime = DateTime.Now;

            _context.Materials.Update(materialAboutToBeUpdated);
            materialAboutToBeUpdated.material_name = entry.material_name;
            materialAboutToBeUpdated.amount = entry.amount;
            materialAboutToBeUpdated.amount_measurement = entry.amount_measurement;
            materialAboutToBeUpdated.lastModifiedDate = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Update " + material_id);
            return Ok(new { message = "Material updated." });
        }
        [HttpPatch("{material_id}/ingredients/{material_ingredient_id}")]
        public async Task<IActionResult> UpdateMaterialIngredient(string material_id, string material_ingredient_id, PatchMaterialIngredients entry)
        {
            Materials? materialAboutToBeUpdated = await _context.Materials.FindAsync(material_id);
            if (materialAboutToBeUpdated == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialAboutToBeUpdated.isActive == false) { return NotFound(new { message = "Specified material entry with the selected material_id not found or deleted." }); }
            MaterialIngredients? materialIngredientAboutToBeUpdated = await _context.MaterialIngredients.FindAsync(material_ingredient_id);
            if (materialIngredientAboutToBeUpdated == null) { return NotFound(new { message = "Specified ingredient entry with the selected ingredient_id not found." }); }
            if (materialIngredientAboutToBeUpdated.isActive == false) { return NotFound(new { message = "Specified ingredient entry with the selected ingredient_id not found or deleted." }); }

            //Add code to check if the item_id on the entry exists in the inventory table
            if (entry.item_id == null) { return NotFound(new { message = "Specified item entry with the inputted item_id not found." }); }

            DateTime currentTime = DateTime.Now;

            _context.MaterialIngredients.Update(materialIngredientAboutToBeUpdated);
            materialIngredientAboutToBeUpdated.item_id = entry.item_id;
            materialIngredientAboutToBeUpdated.amount = entry.amount;
            materialIngredientAboutToBeUpdated.amount_measurement = entry.amount_measurement;
            materialIngredientAboutToBeUpdated.lastModifiedDate = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Update Material " + material_id + " - Ingredient " + material_ingredient_id);
            return Ok(new { message = "Material ingredient updated." });
        }

        //DELETE
        [HttpDelete("{material_id}")]
        public async Task<IActionResult> DeleteMaterial(string material_id)
        {
            //Deleting materials will also delete all of the associated material ingredient

            Materials? materialAboutToBeDeleted = await _context.Materials.FindAsync(material_id);
            if (materialAboutToBeDeleted == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialAboutToBeDeleted.isActive == false) { return NotFound(new { message = "Specified material with the selected material_id already deleted." }); }

            DateTime currentTime = DateTime.Now;
            _context.Materials.Update(materialAboutToBeDeleted);
            materialAboutToBeDeleted.lastModifiedDate = currentTime;
            materialAboutToBeDeleted.isActive = false;

            List<MaterialIngredients> materialIngredientsAboutToBeDeleted = await _context.MaterialIngredients.ToListAsync();
            materialIngredientsAboutToBeDeleted = materialIngredientsAboutToBeDeleted.FindAll(x => x.isActive == true);
            if (materialIngredientsAboutToBeDeleted.IsNullOrEmpty() == false)
            {
                foreach (MaterialIngredients i in materialIngredientsAboutToBeDeleted)
                {
                    _context.MaterialIngredients.Update(i);
                    i.lastModifiedDate = currentTime;
                    i.isActive = false;
                }
            }
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE, Delete Material " + material_id);
            return Ok(new { message = "Material deleted." });
        }
        [HttpDelete("{material_id}/ingredients/{material_ingredient_id}")]
        public async Task<IActionResult> DeleteMaterialIngredient(string material_id, string material_ingredient_id)
        {
            Materials? materialEntry = await _context.Materials.FindAsync(material_id);
            if (materialEntry == null) { return NotFound(new { message = "Specified material with the selected material_id not found." }); }
            if (materialEntry.isActive == false) { return NotFound(new { message = "Specified material with the selected material_id not found or deleted." }); }
            MaterialIngredients? materialIngredientAboutToBeDeleted = await _context.MaterialIngredients.FindAsync(material_ingredient_id);
            if (materialIngredientAboutToBeDeleted == null) { return NotFound(new { message = "Specified material ingredient with the selected material_ingredient_id not found." }); }
            if (materialIngredientAboutToBeDeleted.isActive == false) { return NotFound(new { message = "Specified material ingredient with the selected material_ingredient_id already deleted." }); }

            DateTime currentTime = DateTime.Now;
            _context.MaterialIngredients.Update(materialIngredientAboutToBeDeleted);
            materialIngredientAboutToBeDeleted.lastModifiedDate = currentTime;
            materialIngredientAboutToBeDeleted.isActive = false;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE, Material " + material_id + " - Ingredient " + material_ingredient_id);
            return Ok(new { message = "Material ingredient deleted." });
        }
    }

    [ApiController]
    [Route("BOM/ingredients/")]
    [Authorize(Roles = UserRoles.Admin)]
    public class IngredientController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly IActionLogger _actionLogger;

        public IngredientController(DatabaseContext context, IActionLogger logs) { _context = context; _actionLogger = logs; }

        // GET
        [HttpGet]
        public async Task<List<GetIngredients>> GetAllIngredients()
        {
            List<GetIngredients> response = new List<GetIngredients>();

            List<Ingredients> dbIngredients = await _context.Ingredients.ToListAsync();
            dbIngredients = dbIngredients.FindAll(x => x.isActive == true);

            if (dbIngredients.IsNullOrEmpty() == true) { response.Add(GetIngredients.DefaultResponse()); return response; }

            foreach (Ingredients i in dbIngredients)
            {
                GetIngredients newRow = new GetIngredients(i.ingredient_id, i.item_id, i.amount, i.amount_measurement, i.dateAdded, i.lastModifiedDate);
                response.Add(newRow);
            }

            await _actionLogger.LogAction(User, "GET, All ingredients");

            return response;
        }
        [HttpGet("{ingredient_id}")]
        public async Task<GetIngredients> GetIngredient(string ingredient_id)
        {
            Ingredients? currentIngredient = await _context.Ingredients.FindAsync(ingredient_id);

            if (currentIngredient == null) { return GetIngredients.DefaultResponse(); }
            if (currentIngredient.isActive == false) { return GetIngredients.DefaultResponse(); }

            await _actionLogger.LogAction(User, "GET, Ingredient " +  ingredient_id);
            return new GetIngredients(
                currentIngredient.ingredient_id,
                currentIngredient.item_id,
                currentIngredient.amount,
                currentIngredient.amount_measurement,
                currentIngredient.dateAdded,
                currentIngredient.lastModifiedDate
                );
        }
        [HttpGet("{ingredient_id}/{column_name}")]
        public async Task<object> GetIngredientColumn(string ingredient_id, string column_name)
        {
            Ingredients? currentIngredient = await _context.Ingredients.FindAsync(ingredient_id);
            Object? response = null;

            if (currentIngredient == null) { return NotFound(new { message = "Specified material with the material_id is not found." }); }
            if (currentIngredient.isActive == false) { return NotFound(new { message = "Specified material with the material_id is not found or deleted." }); }

            switch (column_name)
            {
                case "ingredient_id": response = currentIngredient.ingredient_id; break;
                case "item_id": response = currentIngredient.item_id; break;
                case "amount": response = currentIngredient.amount; break;
                case "amount_measurement": response = currentIngredient.amount_measurement; break;
                case "dateAdded": response = currentIngredient.dateAdded; break;
                case "lastModifiedDate": response = currentIngredient.lastModifiedDate; break;
                default: response = BadRequest(new { message = "Specified column does not exist." }); break;
            }

            await _actionLogger.LogAction(User, "GET, Ingredient " + ingredient_id + " - Column " +  column_name);
            return response;
        }

        // POST
        [HttpPost]
        public async Task<IActionResult> AddIngredient(PostIngredients data)
        {
            try
            {
                if (data == null) { return BadRequest(new { message = "Data to be inserted is empty." }); }

                //Check if item id exists on the 'Materials' table
                //or in the inventory
                var materialIdList = await _context.Materials.Select(id => id.material_id).ToListAsync();
                //Add additional code here for inventory id checking
                if (materialIdList.Find(id => id == data.item_id).IsNullOrEmpty() == true && false) { return NotFound(new { message = "Id specified in the request does not exist in the database." }); }

                string lastIngredientId = "";

                try { List<Ingredients> x = await _context.Ingredients.ToListAsync(); lastIngredientId = x.Last().ingredient_id; }
                catch (Exception ex)
                {
                    string newIngredientId = IdFormat.ingredientIdFormat;
                    for (int i = 1; i <= IdFormat.idNumLength; i++) { newIngredientId += "0"; }
                    lastIngredientId = newIngredientId;
                }

                Ingredients newIngredientsEntry = new Ingredients();
                DateTime currentTime = DateTime.Now;

                newIngredientsEntry.ingredient_id = IdFormat.IncrementId(IdFormat.ingredientIdFormat, IdFormat.idNumLength, lastIngredientId);


                newIngredientsEntry.item_id = data.item_id;

                newIngredientsEntry.amount = data.amount;
                newIngredientsEntry.amount_measurement = data.amount_measurement;
                newIngredientsEntry.isActive = true;
                newIngredientsEntry.dateAdded = currentTime;
                newIngredientsEntry.lastModifiedDate = currentTime;

                await _context.Ingredients.AddAsync(newIngredientsEntry);
                await _context.SaveChangesAsync();

                await _actionLogger.LogAction(User, "POST, Add Ingredient " + newIngredientsEntry.ingredient_id);
                return Ok(new { message = "Data inserted to the database." });
            }
            catch (Exception e)
            {
                return StatusCode(500, "Error on creating new ingredient.");
            }


        }

        // PATCH
        [HttpPatch("{ingredient_id}")]
        public async Task<IActionResult> UpdateIngredients(string ingredient_id, PatchIngredients entry)
        {
            Ingredients? ingredientAboutToBeUpdated = await _context.Ingredients.FindAsync(ingredient_id);
            if (ingredientAboutToBeUpdated == null) { return NotFound(new { message = "Specified ingredient entry with the selected ingredient_id not found." }); }
            if (ingredientAboutToBeUpdated.isActive == false) { return NotFound(new { message = "Specified ingredient entry with the selected ingredient_id not found or deleted." }); }

            //Code for checking if the id in the request exists in the material or inventory tables
            var materialIdList = await _context.Materials.Select(id => id.material_id).ToListAsync();
            //Add additional code here to check the inventory
            if (materialIdList.Find(id => id == entry.item_id).IsNullOrEmpty() == true && false) { return NotFound(new { message = "Id specified in the request does not exist in the database." }); }

            DateTime currentTime = DateTime.Now;

            _context.Ingredients.Update(ingredientAboutToBeUpdated);
            ingredientAboutToBeUpdated.item_id = entry.item_id;
            ingredientAboutToBeUpdated.amount = entry.amount;
            ingredientAboutToBeUpdated.amount_measurement = entry.amount_measurement;
            ingredientAboutToBeUpdated.lastModifiedDate = currentTime;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH, Update Ingredient " + ingredient_id);
            return Ok(new { message = "Ingredient updated" });
        }

        // DELETE
        [HttpDelete("{ingredient_id}")]
        public async Task<IActionResult> DeleteIngredient(string ingredient_id)
        {
            Ingredients? ingredientAboutToBeDeleted = await _context.Ingredients.FindAsync(ingredient_id);
            if (ingredientAboutToBeDeleted == null) { return NotFound(new { message = "Specified ingredient with the selected ingredient_id not found." }); }
            if (ingredientAboutToBeDeleted.isActive == false) { return NotFound(new { message = "Specified ingredient with the selected ingredient_id already deleted." }); }

            DateTime currentTime = DateTime.Now;
            _context.Ingredients.Update(ingredientAboutToBeDeleted);
            ingredientAboutToBeDeleted.lastModifiedDate = currentTime;
            ingredientAboutToBeDeleted.isActive = false;
            await _context.SaveChangesAsync();

            await _actionLogger.LogAction(User, "DELETE, Delete Ingredient " + ingredient_id);
            return Ok(new { message = "Ingredient deleted." });

        }
    }
}
