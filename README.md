### **HOW TO RUN**
1. Go to appsettings.json
2. Change the connection strings in the `SQLServerMigrationTesting` and `AUTHTESTING` to the database that you want to insert the tables
   
> If the connection string in `AUTHTESTING` points to a SQLServer and not to a MySQLServer
   > 1. Go to program.cs
   > 2. Change `builder.Services.AddDbContext<AuthDB>(options => options.UseMySql(builder.Configuration.GetConnectionString("AUTHTESTING"), serverVersion));`
      **to** `builder.Services.AddDbContext<AuthDB>(optionsAction: options => options.UseSqlServer(builder.Configuration.GetConnectionString("AUTHTESTING")));`

3. Migrate all of the database context
   1. Go to package manager console
   2. Run the following commands for the 3 database contexts `DatabaseContext`, `LoggingDatabaseContext`, and `AuthDB`
   3. `Add-Migration -Context <insert_context_name_here> -Name <insert_name_here>`
   4. `Update-Database -Context <insert_context_name_here>`
      
   > E.G.
   > 1. `Add-Migration -Context LoggingDatabaseContext -Name InitialMigration_LoggingDatabaseContext`
   > 2. `Update-Database -Context LoggingDatabaseContext`
      
