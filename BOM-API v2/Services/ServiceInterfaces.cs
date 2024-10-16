using BillOfMaterialsAPI.Schemas;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace BOM_API_v2.Services
{
    public interface IActionLogger
    {
        Task<int> LogAction(ClaimsPrincipal user, string transaction_type, string transaction_description);

        Task<int> LogUserLogin(IdentityUser user);
    }
    public interface IEmailService
    {
        Task<int> SendEmailConfirmationEmail(string recepientName, [EmailAddress] string recepientEmail, [Url] string confirmEmailLink);
        Task<int> SendForgotPasswordEmail(string recepientName, [EmailAddress] string recepientEmail, [Url] string confirmEmailLink);
        Task<int> SendPaymentNoticeToEmail(string recipientName, [EmailAddress] string recipientEmail, string checkoutUrl);
    }

    //Tightly Connected
    public interface ICakePriceCalculator
    {
        Task<double> CalculateSubMaterialCost(MaterialIngredients data);
        Task<double> CalculateSubMaterialCost(Ingredients data);
    }
}
