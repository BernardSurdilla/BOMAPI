using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UnitsNet;

namespace BOM_API_v2.Controllers
{
    [ApiController]
    [Route("BOM/ui_helpers/")]
    [Authorize(Roles = UserRoles.Admin)]
    public class UIHelpersController: ControllerBase
    {
        [HttpGet("valid_measurement_values")]
        public async Task<Dictionary<string, List<string>>> ValidMeasurementValues()
        {
            Dictionary<string, List<string>> response = new Dictionary<string, List<string>>();


            string[] validQuantities = ["Mass", "Volume"];
            foreach (string currentQuantity in validQuantities)
            {
                List<string> currentQuantityUnits = new List<string>();
                foreach (UnitInfo currentUnit in Quantity.ByName[currentQuantity].UnitInfos)
                {
                    currentQuantityUnits.Add(currentUnit.Name);
                }
                response.Add(currentQuantity, currentQuantityUnits);
            }

            return response;
        }
        [HttpGet("valid_item_types")]
        public async Task<string[]> ValidItemTypes()
        {
            return ["MAT", "INV"];
        }
    }
}
