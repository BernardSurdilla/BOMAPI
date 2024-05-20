using BillOfMaterialsAPI.Helpers;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UnitsNet;

namespace BOM_API_v2.Controllers
{
    [ApiController]
    [Route("BOM/ui_helpers/")]
    [Authorize]
    public class UIHelpersController: ControllerBase
    {
        [HttpGet("valid_measurement_values")]
        public async Task<Dictionary<string, List<string>>> ValidMeasurementValues()
        {
            return ValidUnits.ValidMeasurementUnits();
        }
        [HttpGet("valid_item_types")]
        public async Task<string[]> ValidItemTypes()
        {
            return ["MAT", "INV"];
        }
    }
}
