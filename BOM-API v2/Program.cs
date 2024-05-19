using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Services;
using BOM_API_v2.Services;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using Microsoft.EntityFrameworkCore.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "DebugPolicy", policy =>
    {
        policy.SetIsOriginAllowedToAllowWildcardSubdomains()
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
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
builder.Services.AddDbContext<DatabaseContext>(options => options.UseMySql(builder.Configuration.GetConnectionString("ProgramDB"), serverVersion));
builder.Services.AddDbContext<LoggingDatabaseContext>(options => options.UseMySql(builder.Configuration.GetConnectionString("ProgramDB"), serverVersion));
builder.Services.AddDbContext<AuthDB>(options => options.UseMySql(builder.Configuration.GetConnectionString("AUTHTESTING"), serverVersion));
builder.Services.AddDbContext<KaizenTables>(options => options.UseMySql(builder.Configuration.GetConnectionString("connection"), serverVersion));

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
});

//Custom Services
//NOTE: Services can also be dependency injected
//E.G: Using a DBContext in the constructor to get a database context

builder.Services.AddTransient<IActionLogger, AccountManager>(); //Logging service
builder.Services.AddTransient<IEmailService, EmailService>(); //Email Sending Service


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
