using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Services;
using BOM_API_v2.Bridge;
using BOM_API_v2.Helpers;
using BOM_API_v2.Services;
using JWTAuthentication.Authentication;
using LiveChat;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;


const string API_VERSION = "v1";
const string APP_CONTEXT = "culo-api";
const string GLOBAL_ROUTE_PREFIX = APP_CONTEXT + "/" + API_VERSION;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "DebugPolicy", policy =>
    {
        policy.SetIsOriginAllowedToAllowWildcardSubdomains()
        .SetIsOriginAllowed((host) => true)
        .AllowCredentials()
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
    options.AddPolicy(name: "FrontEndAndOriginOnly", policy =>
    {
        policy.SetIsOriginAllowed((host) => true)
        .WithOrigins("https://culo-t97g.vercel.app/")
        .AllowCredentials()
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});
builder.Services.AddControllers(options => options.Conventions.Add(new GlobalControllerRoutePrefixConvention(new GlobalControllerRoutePrefix(GLOBAL_ROUTE_PREFIX))))
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
;


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    opt.OperationFilter<SecurityRequirementsOperationFilter>();
});


//
// SERVER CONNECTIONS
//

var serverVersion = new MariaDbServerVersion(new Version(10, 4, 28));

builder.Services.AddDbContext<DatabaseContext>(options => options.UseMySql(builder.Configuration.GetConnectionString("ProgramDB"), serverVersion, mysqlOptions => mysqlOptions.UseNewtonsoftJson()));
builder.Services.AddDbContext<LoggingDatabaseContext>(options => options.UseMySql(builder.Configuration.GetConnectionString("ProgramDB"), serverVersion));
builder.Services.AddDbContext<AuthDB>(options => options.UseMySql(builder.Configuration.GetConnectionString("AUTHTESTING"), serverVersion));
builder.Services.AddDbContext<DirectMessagesDB>(options => options.UseMySql(builder.Configuration.GetConnectionString("ProgramDB"), serverVersion));

builder.Services.AddDbContext<KaizenTables>(options => options.UseMySql(builder.Configuration.GetConnectionString("connection"), serverVersion));
builder.Services.AddDbContext<InventoryAccounts>(options => options.UseMySql(builder.Configuration.GetConnectionString("connection"), serverVersion));

//Use in memory db for testing
//builder.Services.AddDbContext<KaizenTables>(options => options.UseInMemoryDatabase("DBTest"));
//builder.Services.AddDbContext<DatabaseContext>(options => options.UseInMemoryDatabase("DBTest"));

builder.Services.AddAuthorization();

builder.Services.AddIdentity<APIUsers, IdentityRole>()
    .AddEntityFrameworkStores<AuthDB>()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidAudience = builder.Configuration.GetSection("JWT").GetValue<string>("ValidAudience"),
        ValidIssuer = builder.Configuration.GetSection("JWT").GetValue<string>("ValidIssuer"),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration.GetSection("JWT").GetValue<string>("Secret")))
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
                return Task.CompletedTask;
            }
            else
            {
                string? accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/live-chat")))
                {
                    context.Token = accessToken.Split(" ").Last();
                }
                return Task.CompletedTask;
            }
        }
    };
});

//Live Chat
builder.Services.AddSignalR();

//Custom Services
//NOTE: Services can also be dependency injected
//E.G: Using a DBContext in the constructor to get a database context

builder.Services.AddScoped<IActionLogger, AccountManager>(); //Logging service
builder.Services.AddTransient<IEmailService, EmailService>(); //Email Sending Service
builder.Services.AddTransient<IInventoryBOMBridge, BOMInventoryBridge>(); //Inventory BOM Bridge Service
builder.Services.AddSingleton<ILiveChatConnectionManager, LiveChatConnectionManager>(); //Live chat connections

builder.Services.AddRateLimiter(_ => _
    .AddFixedWindowLimiter(policyName: "fixed", options =>
    {
        options.PermitLimit = 4;
        options.Window = TimeSpan.FromSeconds(12);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 2;
    }));

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger()
    .UseSwaggerUI()
    .UseCors("FrontEndAndOriginOnly");
//}

app.UseRateLimiter();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/live-chat");

app.Run();
