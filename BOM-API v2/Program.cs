using BillOfMaterialsAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using BillOfMaterialsAPI.Services;
using BillOfMaterialsAPI.Schemas;
using JWTAuthentication;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "DebugPolicy", policy => { 
        policy.AllowAnyOrigin(); 
        policy.AllowAnyHeader(); });
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    opt.OperationFilter<SecurityRequirementsOperationFilter>();
});

//
// SERVER CONNECTIONS
//
//SqlServer Connection
builder.Services.AddDbContext<DatabaseContext>(optionsAction: options => options.UseSqlServer(builder.Configuration.GetConnectionString("SQLServerMigrationTesting")));
builder.Services.AddDbContext<LoggingDatabaseContext>(optionsAction: options => options.UseSqlServer(builder.Configuration.GetConnectionString("SQLServerMigrationTesting")));
//NEW AUTHENTICATION METHOD
var serverVersion = new MariaDbServerVersion(new Version(10, 4, 28));
builder.Services.AddDbContext<AuthDB>(options => options.UseMySql(builder.Configuration.GetConnectionString("AUTHTESTING"), serverVersion));

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
});

//Custom Services
//NOTE: Services can also be dependency injected
//E.G: Using a DBContext in the constructor to get a database context

builder.Services.AddTransient<IActionLogger, AccountManager>(); //Logging service


var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger()
    .UseSwaggerUI()
    .UseCors("DebugPolicy");
}
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
