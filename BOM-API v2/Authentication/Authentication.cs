using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Helpers;
using BillOfMaterialsAPI.Services;


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



namespace JWTAuthentication.Authentication
{

    //New Authentication
    public class AuthDB : IdentityDbContext<APIUsers>
    {
        public AuthDB(DbContextOptions<AuthDB> options) : base(options)
        {

        }

    }

    public class APIUsers : IdentityUser
    {
        [Required] public DateTime JoinDate { get; set; }
    }
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Artist = "Artist";
        public const string Customer = "Customer";
    }
    public class RegisterModel
    {
        [Required(ErrorMessage = "User Name is required")]
        public string Username { get; set; }

        [EmailAddress]
        [Required(ErrorMessage = "Email is required")]
        public string Email { get; set; }

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
        private readonly IConfiguration _configuration;
        private readonly IActionLogger _actionLogger;
        private readonly IEmailService _emailService;

        public AuthenticateController(UserManager<APIUsers> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, IActionLogger actionLogger, IEmailService emailService)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            _configuration = configuration;
            _actionLogger = actionLogger;
            _emailService = emailService;
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
            if (userExists != null || userEmailExist != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "User already exists!" });

            APIUsers user = new APIUsers()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Username,
                JoinDate = DateTime.Now
            };

            var result = await userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." });

            if (!await roleManager.RoleExistsAsync(UserRoles.Customer))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Customer));
            if (await roleManager.RoleExistsAsync(UserRoles.Customer))
            {
                await userManager.AddToRoleAsync(user, UserRoles.Customer);
            }

            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }
        [HttpPost("register_artist/")]
        public async Task<IActionResult> RegisterArtist([FromBody] RegisterModel model)
        {
            var userExists = await userManager.FindByNameAsync(model.Username);
            var userEmailExist = await userManager.FindByEmailAsync(model.Email);
            if (userExists != null || userEmailExist != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "User already exists!" });

            APIUsers user = new APIUsers()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Username,
                JoinDate = DateTime.Now
            };

            var result = await userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." });

            if (!await roleManager.RoleExistsAsync(UserRoles.Artist))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Artist));
            if (await roleManager.RoleExistsAsync(UserRoles.Artist))
            {
                await userManager.AddToRoleAsync(user, UserRoles.Artist);
            }

            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }
        [HttpPost("register_admin/")]
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterModel model)
        {
            var userExists = await userManager.FindByNameAsync(model.Username);
            if (userExists != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "User already exists!" });

            APIUsers user = new APIUsers()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Username,
                JoinDate = DateTime.Now
            };

            var result = await userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { Status = "Error", Message = "User creation failed! Please check user details and try again." });

            if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));
            if (await roleManager.RoleExistsAsync(UserRoles.Admin))
            {
                await userManager.AddToRoleAsync(user, UserRoles.Admin);
            }

            return Ok(new Response { Status = "Success", Message = "User created successfully!" });
        }

        [Authorize][HttpPost("send_confirmation_email")]
        public async Task<IActionResult> SendEmailConfirmationEmail()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return NotFound(new { message = "User not found." }); }

            string? userName = currentUser.UserName;
            string? email = currentUser.Email;

            int result = await _emailService.SendEmailConfirmationEmail(userName, email, "https://www.google.com");
            if (result == 0)
            {
                return Ok(new { message = "Email sent to " + email });
            }
            else
            {
                return StatusCode(500, new { message = "Email failed to send to " + email });
            }

            
        }

        [Authorize][HttpGet("current_user/")]
        public async Task<GetUser> CurrentUser()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return new GetUser(); }

            GetUser response = new GetUser();
            response.user_id = currentUser.Id;
            response.email = currentUser.Email;
            response.username = currentUser.UserName;
            response.is_email_confirmed = currentUser.EmailConfirmed;
            response.join_date = currentUser.JoinDate;

            response.roles = (List<string>)await userManager.GetRolesAsync(currentUser);

            await _actionLogger.LogAction(User, "GET, User Information " + currentUser.Id);
            return response;
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
            await _actionLogger.LogAction(User, "GET, All User Information " + currentUser.Id);
            return response;
        }

        
    }
    
}
