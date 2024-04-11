# **HOW TO RUN**
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
      

## BOM Setup

PastryMaterials 
- Base record for the cake ingredients, connected to a design record, all records have ingredients connected to it in the 'Ingredients' table

Ingredients 
- This contains all records for the ingredients of a cake, all records here pertain to a 'PastryMaterials' record
- Records here contain the amount and measurement for the ingredient (e.g. 2 cups, 1 kilogram)
- Records here can either refer to an item in the inventory, or a material

Materials 
- Base record for preset ingredients for the 'PastryMaterials', records in this table are referred by the records in the 'MaterialIngredients' table
- This table is for presets
- Use case for this is for ingredients that they use in a lot of other cakes that is made with a combination of ingredients such as batters
- The entries here can be referred by the 'Ingredients' table

MaterialIngredients 
- This contains all records for the ingredients of a preset, all records here pertain to a 'Materials' record
- This table is for the ingredients of the presets
- Records here can refer to either an item in the inventory, or a material
- Records here contain the amount and measurement for the material ingredient (e.g. 2 cups, 1 kilogram)

So....
![BOM_TableArch v1](https://github.com/BernardSurdilla/BOMAPI/assets/149220736/e51a6862-9f8c-4169-82f6-996874b03282)

### NOTES:
- You cannot add a material as it's own material ingredient
- You cannot add a material as a material ingredient for a preset if that material contains the preset as its own material ingredient

- When deleting a 'PastryMaterials' record, all 'Ingredients' record(s) for it will be deleted
- When deleting a 'Materials' record, all 'MaterialIngredients' record(s) for it will be deleted, and all 'MaterialIngredients' and 'Ingredients' record(s) that refer to it will be deleted

![SCENARIO1-4](https://github.com/BernardSurdilla/BOMAPI/assets/149220736/e8943862-a3b8-4a3d-a162-68a10a24a68a)

- When restoring a 'PastryMaterials' record, all 'Ingredients' records associated with it will be recovered as well unless...
  - It refers to a 'Materials' record that is deleted
  - It refers to a item in the inventory is deleted
- When restoring a 'Materials' record, all 'MaterialIngredients' records associated with it will be recovered as well unless...
  - It refers to a 'Materials' record that is deleted
  - It refers to a item in the inventory is deleted

![Scenarios5-6](https://github.com/BernardSurdilla/BOMAPI/assets/149220736/96d4052a-21b6-44f0-9ae1-135d68b584f4)
  
- When restoring a 'Materials' record, you can also have it restore all 'MaterialIngredients' for other 'Materials' that refer to it, but will fail if the 'Materials' record for it is deleted
- When restoring a 'Materials' record, you can also have it restore all 'Ingredients' that refer to it, but will fail if the 'PastryMaterials' record for it is deleted

![Scenarios7-8](https://github.com/BernardSurdilla/BOMAPI/assets/149220736/c4e767f8-647b-4a91-9474-04df1fdf5d2b)

    
