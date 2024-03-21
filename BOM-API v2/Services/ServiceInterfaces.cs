using System.Security.Claims;

namespace BillOfMaterialsAPI.Services
{
    public interface IActionLogger
    {
        Task<int> LogAction(ClaimsPrincipal user, string action);
    }
}
