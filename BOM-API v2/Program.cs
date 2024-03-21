using BillOfMaterialsAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using BillOfMaterialsAPI.Services;
using BillOfMaterialsAPI.Schemas;

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

//Connection string
//var connectionString = "server=host.docker.internal;user=root;database=thesisdbtest";

//MySql server Connection
//Links the database context class to the database
//var serverVersion = new MariaDbServerVersion(new Version(10, 4, 28));
//builder.Services.AddDbContext<AccountDatabaseContext>(options => options.UseMySql(builder.Configuration.GetConnectionString("MySql"), serverVersion));
//builder.Services.AddDbContext<DatabaseContext>(options => options.UseMySql(builder.Configuration.GetConnectionString("MySql"), serverVersion));

//Account Database Server 
//var serverVersion = new MariaDbServerVersion(new Version(10, 4, 28));
//builder.Services.AddDbContext<AccountDatabaseContext>(options => options.UseMySql(builder.Configuration.GetConnectionString("MySql"), serverVersion));
//Logging
//builder.Services.AddDbContext<LoggingDatabaseContext>(options => options.UseMySql(builder.Configuration.GetConnectionString("MySql"), serverVersion));



//
// SERVER CONNECTIONS
//


//SqlServer Connection
builder.Services.AddDbContext<DatabaseContext>(optionsAction: options => options.UseSqlServer(builder.Configuration.GetConnectionString("SQLServerMigrationTesting")));

//builder.Services.AddIdentity<Users, >(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<AccountDatabaseContext>();
builder.Services.AddDbContext<LoggingDatabaseContext>(optionsAction: options => options.UseSqlServer(builder.Configuration.GetConnectionString("SQLServerMigrationTesting")));
builder.Services.AddDbContext<AccountDatabaseContext>(optionsAction: options => options.UseSqlServer(builder.Configuration.GetConnectionString("SQLServerMigrationTesting")));


//Security
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<Users>(opt => {

    opt.Password.RequiredLength = 8;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequireUppercase = true;
    opt.Password.RequireLowercase = true;
    opt.Password.RequiredUniqueChars = 1;

    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(2);
    opt.Lockout.MaxFailedAccessAttempts = 5;
    opt.Lockout.AllowedForNewUsers = true;

    opt.User.RequireUniqueEmail = true;
    opt.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

}).AddEntityFrameworkStores<AccountDatabaseContext>(); 
builder.Services.ConfigureApplicationCookie(options => {
    // Cookie settings
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

    options.SlidingExpiration = true;
});


//Custom Services
//NOTE: Services can also be dependency injected
//E.G: Using a DBContext in the constructor to get a database context
builder.Services.AddTransient<IActionLogger, AccountManager>();


var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseCors("DebugPolicy");
}

app.MapIdentityApi<Users>();
app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();

app.Run();
