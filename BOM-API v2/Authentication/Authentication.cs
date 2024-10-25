using BillOfMaterialsAPI.Helpers;
using BOM_API_v2.Bridge;
using BOM_API_v2.Services;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.IdentityModel.Tokens;
using MimeKit.Encodings;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;



namespace JWTAuthentication.Authentication
{

    //New Authentication
    public class AuthDB : IdentityDbContext<APIUsers>
    {
        public AuthDB(DbContextOptions<AuthDB> options) : base(options)
        {

        }
        public DbSet<EmailConfirmationKeys> EmailConfirmationKeys { get; set; } //Table for storing user confirmation keys
        public DbSet<ForgotPasswordKeys> ForgotPasswordKeys { get; set; }
        public DbSet<ProfileImages> ProfileImages { get; set; } //Table for storing user profile images
    }
    public class APIUsers : IdentityUser
    {
        [Required] public DateTime JoinDate { get; set; }
    }
    public class EmailConfirmationKeys
    {
        [Required][ForeignKey("APIUsers")] public string Id { get; set; }
        [Required] public string ConfirmationKey { get; set; }
        [Required] public DateTime ValidUntil { get; set; }
    }
    public class ForgotPasswordKeys
    {
        [Required][ForeignKey("APIUsers")] public string Id { get; set; }
        [Required] public string ForgotPasswordKey { get; set; }
        [Required] public DateTime ValidUntil { get; set; }
    }

    public class ProfileImages
    {
        [Required][ForeignKey("APIUsers")] public string Id { get; set; }
        [Required] public byte[] picture_data { get; set; }
    }

    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Artist = "Artist";
        public const string Customer = "Customer";
        public const string Manager = "Manager";
    }
    public class RegisterModel
    {
        [Required(ErrorMessage = "User Name is required")]
        public string Username { get; set; }

        [EmailAddress]
        [Required(ErrorMessage = "Email is required")]
        public string Email { get; set; }

        [MaxLength(10)] public string ContactNumber { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }
    }
    public class LoginModel
    {
        [EmailAddress]
        [Required(ErrorMessage = "User Name is required")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }
    }
    public class Response
    {
        public string Status { get; set; }
        public string Message { get; set; }
    }

    public class GetUser
    {
        public string user_id { get; set; }
        [EmailAddress] public string email { get; set; }
        public string username { get; set; }
        public List<string> roles { get; set; }
        public string phoneNumber { get; set; }
        public bool isEmailConfirmed { get; set; }
        public DateTime joinDate { get; set; }
    }
    public class PatchUser
    {
        public string username { get; set; }
        public string phoneNumber { get; set; }
    }
    public class ResetPassword
    {
        public string resetPasswordToken { get; set; }
        public string newPassword { get; set; }
    }
}


namespace JWTAuthentication.Controllers
{

    [Route("users")]
    [ApiController]
    public class AuthenticateController : ControllerBase
    {
        private readonly UserManager<APIUsers> userManager;
        private readonly RoleManager<IdentityRole> roleManager;

        private readonly AuthDB _auth;

        private readonly IConfiguration _configuration;
        private readonly IActionLogger _actionLogger;
        private readonly IEmailService _emailService;
        private readonly IInventoryBOMBridge _inventoryBOMBridge;


        public AuthenticateController(UserManager<APIUsers> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, IActionLogger actionLogger, IEmailService emailService, AuthDB auth, IInventoryBOMBridge inventoryBOMBridge)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            _configuration = configuration;
            _actionLogger = actionLogger;
            _emailService = emailService;
            _auth = auth;
            this._inventoryBOMBridge = inventoryBOMBridge;
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var user = await userManager.FindByEmailAsync(model.Email);
            
            if (user != null && await userManager.CheckPasswordAsync(user, model.Password))
            {
                if (user.EmailConfirmed == false)
                {
                    return Unauthorized(new {message = "Please confirm your email first"});
                }

                var userRoles = await userManager.GetRolesAsync(user);

                var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            };
                
                foreach (var userRole in userRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, userRole));
                }

                var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetSection("JWT").GetValue<string>("Secret")));

                var token = new JwtSecurityToken(
                    issuer: _configuration.GetSection("JWT").GetValue<string>("ValidIssuer"),
                    audience: _configuration.GetSection("JWT").GetValue<string>("ValidAudience"),
                    expires: TimeZoneInfo.ConvertTime(DateTime.Now.AddHours(8), TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")),
                    claims: authClaims,
                    signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                    );

                await _actionLogger.LogUserLogin(user);
                return Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(token),
                    expiration = token.ValidTo
                });
            }
            return Unauthorized(new { message = "Incorrect username or password" });
        }
        [HttpPost("register-customer/")]
        public async Task<IActionResult> RegisterCustomer([FromBody] RegisterModel model)
        {
            var userExists = await userManager.FindByNameAsync(model.Username);
            var userEmailExist = await userManager.FindByEmailAsync(model.Email);
            if (userExists != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "Account with specified username already exists!" });
            if (userEmailExist != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "Account with specified email already exists!" });

            APIUsers user = new APIUsers()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Username,
                JoinDate = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")),
                PhoneNumber = model.ContactNumber,
            };

            var result = await userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." });
            else { await _inventoryBOMBridge.AddCreatedAccountToInventoryAccountTables(user, 1); }

            if (!await roleManager.RoleExistsAsync(UserRoles.Customer))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Customer));
            if (await roleManager.RoleExistsAsync(UserRoles.Customer))
            {
                await userManager.AddToRoleAsync(user, UserRoles.Customer);
            }

            var createdAccount = await userManager.FindByEmailAsync(model.Email);
            await SendConfirmationEmail(createdAccount.Id);

            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }
        [HttpPost("register-artist/")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> RegisterArtist([FromBody] RegisterModel model)
        {
            var userExists = await userManager.FindByNameAsync(model.Username);
            var userEmailExist = await userManager.FindByEmailAsync(model.Email);
            if (userExists != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "Account with specified username already exists!" });
            if (userEmailExist != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "Account with specified email already exists!" });

            APIUsers user = new APIUsers()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Username,
                JoinDate = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")),
                PhoneNumber = model.ContactNumber,
            };

            var result = await userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." });
            else { await _inventoryBOMBridge.AddCreatedAccountToInventoryAccountTables(user, 2); }

            if (!await roleManager.RoleExistsAsync(UserRoles.Artist))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Artist));
            if (await roleManager.RoleExistsAsync(UserRoles.Artist))
            {
                await userManager.AddToRoleAsync(user, UserRoles.Artist);
            }

            var createdAccount = await userManager.FindByEmailAsync(model.Email);
            await SendConfirmationEmail(createdAccount.Id);

            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }
        [HttpPost("register-manager/")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> RegisterManager([FromBody] RegisterModel model)
        {
            var userExists = await userManager.FindByNameAsync(model.Username);
            var userEmailExist = await userManager.FindByEmailAsync(model.Email);
            if (userExists != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "Account with specified username already exists!" });
            if (userEmailExist != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "Account with specified email already exists!" });
            APIUsers user = new APIUsers()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Username,
                JoinDate = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")),
                PhoneNumber = model.ContactNumber,
            };

            var result = await userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." });
            else { await _inventoryBOMBridge.AddCreatedAccountToInventoryAccountTables(user, 4); }

            if (!await roleManager.RoleExistsAsync(UserRoles.Manager))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Manager));
            if (await roleManager.RoleExistsAsync(UserRoles.Manager))
            {
                await userManager.AddToRoleAsync(user, UserRoles.Manager);
            }

            var createdAccount = await userManager.FindByEmailAsync(model.Email);
            await SendConfirmationEmail(createdAccount.Id);

            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }
        [HttpPost("register-admin/")]
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterModel model, string secret_key)
        {
            string configSecretKey = _configuration.GetValue("AdminAccountCreationKey", "");
            if (configSecretKey == "" || configSecretKey != secret_key) { return BadRequest(new { message = "Invalid secret key" }); }

            var userExists = await userManager.FindByNameAsync(model.Username);
            var userEmailExist = await userManager.FindByEmailAsync(model.Email);
            if (userExists != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "Account with specified username already exists!" });
            if (userEmailExist != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "Account with specified email already exists!" });

            APIUsers user = new APIUsers()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Username,
                JoinDate = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")),
                PhoneNumber = model.ContactNumber,
            };

            var result = await userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." });
            else { await _inventoryBOMBridge.AddCreatedAccountToInventoryAccountTables(user, 3); }

            if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));
            if (await roleManager.RoleExistsAsync(UserRoles.Admin))
            {
                await userManager.AddToRoleAsync(user, UserRoles.Admin);
            }

            var createdAccount = await userManager.FindByEmailAsync(model.Email);
            await SendConfirmationEmail(createdAccount.Id);

            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpGet("all-users/")]
        public async Task<List<GetUser>> GetAllUser(int? page, int? record_per_page)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return new List<GetUser>(); }

            List<GetUser> response = new List<GetUser>();

            List<APIUsers> registeredUsers;

            if (page == null) { registeredUsers = await userManager.Users.ToListAsync(); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < Page.DefaultNumberOfEntriesPerPage ? Page.DefaultNumberOfEntriesPerPage : record_per_page.Value;
                int current_page = page.Value < Page.DefaultStartingPageNumber ? Page.DefaultStartingPageNumber : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                registeredUsers = await userManager.Users.Skip(num_of_record_to_skip).Take(record_limit).ToListAsync();
            }

            foreach (APIUsers user in registeredUsers)
            {
                GetUser newResponseEntry = new GetUser();
                newResponseEntry.user_id = user.Id;
                newResponseEntry.username = user.UserName;
                newResponseEntry.email = user.Email;
                newResponseEntry.phoneNumber = user.PhoneNumber;
                newResponseEntry.isEmailConfirmed = user.EmailConfirmed;
                newResponseEntry.joinDate = user.JoinDate;

                newResponseEntry.roles = (List<string>)await userManager.GetRolesAsync(user);

                response.Add(newResponseEntry);
            }
            await _actionLogger.LogAction(User, "GET", "All User Information " + currentUser.Id);
            return response;
        }


        [Authorize]
        [HttpGet("/culo-api/v1/current-user")]
        public async Task<GetUser> CurrentUser()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return new GetUser(); }

            GetUser response = new GetUser();
            response.user_id = currentUser.Id;
            response.email = currentUser.Email;
            response.username = currentUser.UserName;
            response.phoneNumber = currentUser.PhoneNumber;
            response.isEmailConfirmed = currentUser.EmailConfirmed;
            response.joinDate = currentUser.JoinDate;

            response.roles = (List<string>)await userManager.GetRolesAsync(currentUser);

            //await _actionLogger.LogAction(User, "GET", "User Information " + currentUser.Id);
            return response;
        }

        [Authorize]
        [HttpPost("/culo-api/v1/current-user/send-confirmation-email/")]
        public async Task<IActionResult> SendEmailConfirmationEmail()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return NotFound(new { message = "User not found." }); }
            if (currentUser.EmailConfirmed == true) { return BadRequest(new { message = "User's email is already confirmed." }); }


            int result = await SendConfirmationEmail(currentUser.Id);
            if (result == 0)
            {
                return Ok(new { message = "Email sent to " + currentUser.Email });
            }
            else
            {
                return StatusCode(500, new { message = "Email failed to send to " + currentUser.Email });
            }
        }
        [HttpPost("/culo-api/v1/current-user/confirm-email/")]
        public async Task<IActionResult> ConfirmUserEmail(string confirmationCode)
        {
            //var currentUser = await userManager.GetUserAsync(User);
            //if (currentUser == null) { return NotFound(new { message = "User not found." }); }
            //if (currentUser.EmailConfirmed == true) { return BadRequest(new { message = "User's email is already confirmed." }); }

            DateTime currentTime = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));
            EmailConfirmationKeys? currentUserKey = null;

            try { currentUserKey = await _auth.EmailConfirmationKeys.Where(x => x.ConfirmationKey == confirmationCode).FirstAsync(); }
            catch { }

            if (currentUserKey == null) { return BadRequest(new { message = "Please send a confirmation code to the user's email first" }); }
            if (currentUserKey.ValidUntil <= currentTime) { return BadRequest(new { message = "Confirmation key already expired, please send a new confirmation key to the user's email" }); }

            string validationKey = currentUserKey.ConfirmationKey;

            if (confirmationCode != validationKey) { return BadRequest(new { message = "Validation code is incorrect." }); }

            var currentUser = await userManager.FindByIdAsync(currentUserKey.Id);
            if (currentUser == null) { return NotFound(new { message = "User not found." }); }
            if (currentUser.EmailConfirmed == true) { return BadRequest(new { message = "User's email is already confirmed." }); }

            if (confirmationCode == validationKey)
            {
                currentUser.EmailConfirmed = true;
            }
            await userManager.UpdateAsync(currentUser);

            _auth.EmailConfirmationKeys.Remove(currentUserKey);
            await _auth.SaveChangesAsync();

            return Ok(new { message = "Email confirmed successfully" });
        }


        [HttpPost("send-forgot-password-email")]
        public async Task<IActionResult> SendForgotPasswordEmailToEmail([FromBody] string email)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user != null)
            {
                await SendForgotPasswordEmail(user.Id);
            }

            return Ok(new { message = "Email is sent to the email address if an account with the specified email exists" });
        }
        [HttpPost("/culo-api/v1/current-user/reset-password/")]
        public async Task<IActionResult> ResetUserPassword([FromBody] ResetPassword data)
        {
            data.resetPasswordToken = Uri.UnescapeDataString(data.resetPasswordToken);

            ForgotPasswordKeys? currentKey = await _auth.ForgotPasswordKeys.Where(x => x.ForgotPasswordKey == data.resetPasswordToken).FirstOrDefaultAsync();

            if (currentKey == null) { return Unauthorized(new { message = "Invalid Reset Token" }); }

            var currentUser = await userManager.FindByIdAsync(currentKey.Id);
            if (currentUser == null) { return Unauthorized(new { message = "Invalid internal data" }); }

            try
            {
                IdentityResult result = await userManager.ResetPasswordAsync(currentUser, data.resetPasswordToken, data.newPassword);

                if (result.Succeeded)
                {
                    _auth.ForgotPasswordKeys.Remove(currentKey);
                    await _auth.SaveChangesAsync();
                    return Ok(new { message = "Password updated successfully!" });
                }
                else
                {
                    return BadRequest(new {message = "Invalid data! Please try again."});
                }
            }
            catch
            {
                return BadRequest(new { messaage = "Change password failed!" });
            }
        }

        [Authorize]
        [HttpGet("/culo-api/v1/current-user/profile-picture")]
        public async Task<byte[]?> CurrentUserProfilePicture()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return null; }

            ProfileImages? currentUserImage = null;
            try { currentUserImage = await _auth.ProfileImages.Where(x => x.Id == currentUser.Id).FirstAsync(); }
            catch (Exception ex) { return null; }

            _actionLogger.LogAction(User, "GET", "Profile image for " + currentUser.Id);
            return currentUserImage.picture_data;

        }
        [Authorize]
        [HttpPost("/culo-api/v1/current-user/upload-profile-picture")]
        public async Task<IActionResult> UploadProfilePicture([FromBody] byte[] picture_data)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return BadRequest(new { message = "User not found" }); }

            ProfileImages newImage = new ProfileImages();
            newImage.Id = currentUser.Id;
            newImage.picture_data = picture_data;

            await _auth.ProfileImages.AddAsync(newImage);
            await _auth.SaveChangesAsync();

            await _actionLogger.LogAction(User, "POST", "Upload image for " + currentUser.Id);
            return Ok(new { message = "Image uploaded for " + currentUser.Id });
        }

        [Authorize]
        [HttpPatch("/culo-api/v1/current-user/update/")]
        public async Task<IActionResult> UpdateUser(PatchUser input)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return NotFound(new { message = "User not found" }); }

            currentUser.PhoneNumber = input.phoneNumber;
            currentUser.UserName = input.username;

            if (await _inventoryBOMBridge.UpdateUser(currentUser, input) == 1)
            {
                await userManager.UpdateAsync(currentUser);
                return Ok(new { messsage = "User " + currentUser.Id + " updated" });
            }
            else { return BadRequest(new { message = "Something unexpected occured in saving the account in the inventory accounts" }); }

        }
        [Authorize]
        [HttpPatch("/culo-api/v1/current-user/profile-picture")]
        public async Task<IActionResult> UpdateUserProfileImage([FromBody] byte[] picture_data)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return BadRequest(new { message = "User not found" }); }
            ProfileImages? currentUserImage = null;
            try { currentUserImage = await _auth.ProfileImages.Where(x => x.Id == currentUser.Id).FirstAsync(); }
            catch { return BadRequest(new { message = "User image not found" }); }

            _auth.ProfileImages.Update(currentUserImage);

            currentUserImage.picture_data = picture_data;

            await _auth.SaveChangesAsync();

            await _actionLogger.LogAction(User, "PATCH", "Update image for " + currentUser.Id);
            return Ok(new { message = "Image uploaded for " + currentUser.Id });
        }

        private async Task<int> SendConfirmationEmail(string accountId)
        {
            var currentUser = await userManager.FindByIdAsync(accountId.ToString());
            if (currentUser == null) { return 0; }
            if (currentUser.EmailConfirmed == true) { return 0; }

            string currentEmailConfirmationKey = Regex.Replace(Convert.ToBase64String(new HMACSHA256().Key), @"[^a-zA-Z0-9]", "");

            DateTime currentTime = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));
            EmailConfirmationKeys? currentUserKey = null;

            try { currentUserKey = await _auth.EmailConfirmationKeys.Where(x => x.Id == currentUser.Id).FirstAsync(); }
            catch { }

            if (currentUserKey != null)
            {
                _auth.EmailConfirmationKeys.Update(currentUserKey);
                currentUserKey.ConfirmationKey = currentEmailConfirmationKey;
                currentUserKey.ValidUntil = currentTime.AddDays(_configuration.GetValue<int>("Email:Validation:EmailValidationKeyValidityDays"));
            }
            else
            {
                EmailConfirmationKeys newEmailConfirmationKey = new EmailConfirmationKeys();
                newEmailConfirmationKey.Id = currentUser.Id;
                newEmailConfirmationKey.ConfirmationKey = currentEmailConfirmationKey;
                newEmailConfirmationKey.ValidUntil = currentTime.AddDays(_configuration.GetValue<int>("Email:Validation:EmailValidationKeyValidityDays"));

                _auth.EmailConfirmationKeys.Add(newEmailConfirmationKey);
            }

            await _auth.SaveChangesAsync();

            string? userName = currentUser.UserName;
            string? email = currentUser.Email;
            string? redirectAddress = _configuration.GetValue<string>("Email:Validation:RedirectAddress") + currentEmailConfirmationKey; //Link here to verify the current user, insert key here


            int result = await _emailService.SendEmailConfirmationEmail(userName, email, redirectAddress);
            if (result == 0)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
        private async Task<int> SendForgotPasswordEmail(string accountId)
        {
            var currentUser = await userManager.FindByIdAsync(accountId.ToString());
            if (currentUser == null) { return 0; }

            string currentForgotPasswordKey = await userManager.GeneratePasswordResetTokenAsync(currentUser);

            DateTime currentTime = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));
            ForgotPasswordKeys? currentUserKey = null;

            try { currentUserKey = await _auth.ForgotPasswordKeys.Where(x => x.Id == currentUser.Id).FirstAsync(); }
            catch { }

            if (currentUserKey != null)
            {
                _auth.ForgotPasswordKeys.Update(currentUserKey);
                currentUserKey.ForgotPasswordKey = currentForgotPasswordKey;
                currentUserKey.ValidUntil = currentTime.AddDays(_configuration.GetValue<int>("Email:ForgotPassword:ForgotPasswordKeyValidityDays"));
            }
            else
            {
                ForgotPasswordKeys newForgotPasswordnKey = new ForgotPasswordKeys();
                newForgotPasswordnKey.Id = currentUser.Id;
                newForgotPasswordnKey.ForgotPasswordKey = currentForgotPasswordKey;
                newForgotPasswordnKey.ValidUntil = currentTime.AddDays(_configuration.GetValue<int>("Email:ForgotPassword:ForgotPasswordKeyValidityDays"));

                _auth.ForgotPasswordKeys.Add(newForgotPasswordnKey);
            }

            await _auth.SaveChangesAsync();

            string? userName = currentUser.UserName;
            string? email = currentUser.Email;
            string? redirectAddress = _configuration.GetValue<string>("Email:ForgotPassword:RedirectAddress") + Uri.EscapeDataString(currentForgotPasswordKey); //Link here to verify the current user, insert key here


            int result = await _emailService.SendForgotPasswordEmail(userName, email, redirectAddress);
            if (result == 0)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }
}
