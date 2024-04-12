using BillOfMaterialsAPI.Services;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BillOfMaterialsAPI.Controllers
{

    [ApiController]
    [Route("BOM/logs/")]
    [Authorize(Roles = UserRoles.Admin)]
    public class LoggingController : ControllerBase
    {
        private readonly LoggingDatabaseContext _logs;
        public LoggingController(LoggingDatabaseContext context) { _logs = context; }

        [HttpGet]
        public async Task<List<TransactionLogs>> GetLogs(int? page, int? record_per_page)
        {
            List<TransactionLogs> transactionLogs;

            //Paging algorithm
            if (page == null) { transactionLogs = await _logs.TransactionLogs.OrderByDescending(x => x.date).ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < 10 ? 10 : record_per_page.Value;
                int current_page = page.Value < 1 ? 1 : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                transactionLogs = await _logs.TransactionLogs.OrderByDescending(x => x.date).Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            return transactionLogs;
        }
    }
}
