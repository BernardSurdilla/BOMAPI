using API_TEST.Controllers;
using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BillOfMaterialsAPI.Controllers
{

    [ApiController]
    [Route("BOM/logs/")]
    [Authorize]
    public class LoggingController : ControllerBase
    {
        private readonly LoggingDatabaseContext _logs;
        public LoggingController(LoggingDatabaseContext context) { _logs = context; }

        [HttpGet]
        public async Task<List<TransactionLogs>> GetLogs()
        {
            List<TransactionLogs> transactionLogs = await _logs.TransactionLogs.ToListAsync();
            return transactionLogs;
        }
    }
}
