using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Helpers;
using BillOfMaterialsAPI.Services;
using BOM_API_v2.Bridge;


using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;



namespace JWTAuthentication.Authentication
{

    //New Authentication
    public class AuthDB : IdentityDbContext<APIUsers>
    {
        public AuthDB(DbContextOptions<AuthDB> options) : base(options)
        {

        }
        public DbSet<EmailConfirmationKeys> EmailConfirmationKeys { get; set; } //Table for storing user confirmation keys
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
        public string phone_number { get; set; }
        public bool is_email_confirmed { get; set; }
        public DateTime join_date { get; set; }
    }
}


namespace JWTAuthentication.Controllers
{
    [Route("auth/")]
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
                var userRoles = await userManager.GetRolesAsync(user);

                var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
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
                    expires: DateTime.Now.AddHours(3),
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
            return Unauthorized();
        }

        [HttpPost("register_customer/")]
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
                JoinDate = DateTime.Now,
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
            
            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }
        [HttpPost("register_artist/")]
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
                JoinDate = DateTime.Now,
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

            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }
        [HttpPost("register_manager/")]
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
                JoinDate = DateTime.Now,
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

            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }

        [HttpPost("register_admin/")]
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
                JoinDate = DateTime.Now,
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

            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }

        
        [Authorize(Roles = UserRoles.Admin)][HttpGet("all_users/")]
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
                newResponseEntry.phone_number = user.PhoneNumber;
                newResponseEntry.is_email_confirmed = currentUser.EmailConfirmed;
                newResponseEntry.join_date = user.JoinDate;

                newResponseEntry.roles = (List<string>)await userManager.GetRolesAsync(user);

                response.Add(newResponseEntry);
            }
            await _actionLogger.LogAction(User, "GET", "All User Information " + currentUser.Id);
            return response;
        }

        [Authorize][HttpGet("user/")]
        public async Task<GetUser> CurrentUser()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return new GetUser(); }

            GetUser response = new GetUser();
            response.user_id = currentUser.Id;
            response.email = currentUser.Email;
            response.username = currentUser.UserName;
            response.phone_number = currentUser.PhoneNumber;
            response.is_email_confirmed = currentUser.EmailConfirmed;
            response.join_date = currentUser.JoinDate;

            response.roles = (List<string>)await userManager.GetRolesAsync(currentUser);

            await _actionLogger.LogAction(User, "GET", "User Information " + currentUser.Id);
            return response;
        }
        [Authorize][HttpPost("user/send_confirmation_email/")]
        public async Task<IActionResult> SendEmailConfirmationEmail()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return NotFound(new { message = "User not found." }); }
            if (currentUser.EmailConfirmed == true) { return BadRequest(new { message = "User's email is already confirmed." }); }

            string currentEmailConfirmationKey = Convert.ToBase64String(new HMACSHA256().Key);

            DateTime currentTime = DateTime.Now;
            EmailConfirmationKeys? currentUserKey = null;

            try { currentUserKey = await _auth.EmailConfirmationKeys.Where(x => x.Id == currentUser.Id).FirstAsync(); }
            catch {  }

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
                return Ok(new { message = "Email sent to " + email });
            }
            else
            {
                return StatusCode(500, new { message = "Email failed to send to " + email });
            }
        }
        [Authorize][HttpPost("user/confirm_email/")]
        public async Task<IActionResult> ConfirmUserEmail(string confirmationCode)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return NotFound(new { message = "User not found." }); }
            if (currentUser.EmailConfirmed == true) { return BadRequest(new { message = "User's email is already confirmed." }); }

            DateTime currentTime = DateTime.Now;
            EmailConfirmationKeys? currentUserKey = null;

            try { currentUserKey = await _auth.EmailConfirmationKeys.Where(x => x.Id == currentUser.Id).FirstAsync(); }
            catch { }

            if (currentUserKey == null) { return BadRequest(new { message = "Please send a confirmation code to the user's email first" }); }
            if (currentUserKey.ValidUntil <= currentTime) { return BadRequest(new { message = "Confirmation key already expired, please send a new confirmation key to the user's email" }); }

            string validationKey = currentUserKey.ConfirmationKey;

            if (confirmationCode != validationKey) { return BadRequest(new { message = "Validation code is incorrect." }); }
            
            if (confirmationCode == validationKey) 
            {
                currentUser.EmailConfirmed = true;
            }
            await userManager.UpdateAsync(currentUser);

            _auth.EmailConfirmationKeys.Remove(currentUserKey);
            await _auth.SaveChangesAsync();

            return Ok(new { message = "Email confirmed successfully" });
        }

        [Authorize][HttpGet("user/profile_picture")]
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
        [Authorize][HttpPost("user/upload_profile_picture")]
        public async Task<IActionResult> UploadProfilePicture([FromBody] byte[] picture_data)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return BadRequest(new { message = "User not found" }); }

            ProfileImages newImage = new ProfileImages();
            newImage.Id = currentUser.Id;
            newImage.picture_data = picture_data;

            await _auth.ProfileImages.AddAsync(newImage);
            await _auth.SaveChangesAsync();

            _actionLogger.LogAction(User, "POST", "Upload image for " + currentUser.Id);
            return Ok(new { message = "Image uploaded for " + currentUser.Id });
        }
    }
}
