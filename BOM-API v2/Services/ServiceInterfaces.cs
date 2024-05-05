using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace BillOfMaterialsAPI.Services
{
    public interface IActionLogger
    {
        Task<int> LogAction(ClaimsPrincipal user, string transaction_type, string transaction_description);

        Task<int> LogUserLogin(IdentityUser user);
    }
    public interface IEmailService
    {
        Task<int> SendEmailConfirmationEmail(string recepientName, [EmailAddress] string recepientEmail, [Url] string confirmEmailLink);
    }
}
