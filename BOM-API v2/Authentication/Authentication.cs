using BillOfMaterialsAPI.Services;
//Controller imports
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
        public string phone_number { get; set; }
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

        public AuthenticateController(UserManager<APIUsers> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, IActionLogger actionLogger)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            _configuration = configuration;
            _actionLogger = actionLogger;
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

        [Authorize][HttpGet("current_user/")]
        public async Task<GetUser> CurrentUser()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return new GetUser(); }

            GetUser response = new GetUser();
            response.user_id = currentUser.Id;
            response.email = currentUser.Email;
            response.username = currentUser.UserName;
            response.join_date = currentUser.JoinDate;

            await _actionLogger.LogAction(User, "GET, User Information " + currentUser.Id);
            return response;
        }
        [Authorize(Roles = UserRoles.Admin)][HttpGet("all_users/")]
        public async Task<List<GetUser>> GetAllUser()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) { return new List<GetUser>(); }

            List<GetUser> response = new List<GetUser>();

            List<APIUsers> registeredUsers = await userManager.Users.ToListAsync();
            foreach (APIUsers user in registeredUsers)
            {
                GetUser newResponseEntry = new GetUser();
                newResponseEntry.user_id = user.Id;
                newResponseEntry.username = user.UserName;
                newResponseEntry.email = user.Email;
                newResponseEntry.phone_number = user.PhoneNumber;
                newResponseEntry.join_date = user.JoinDate;

                response.Add(newResponseEntry);
            }
            await _actionLogger.LogAction(User, "GET, All User Information " + currentUser.Id);
            return response;
        }
    }
    
}
