using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BillOfMaterialsAPI.Services;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BOM_API_v2.Bridge
{
    public class BOMInventoryBridge : IInventoryBOMBridge
    {
        private readonly InventoryAccounts _inventoryAccounts;

        private readonly UserManager<APIUsers> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public BOMInventoryBridge(InventoryAccounts inventoryAccounts, UserManager<APIUsers> userManager, RoleManager<IdentityRole> roleManager)
        {
            _inventoryAccounts = inventoryAccounts;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<int> AddCreatedAccountToInventoryAccountTables(APIUsers newUser, int accessLevel)
        {
            Users newInvUser = new Users();

            newInvUser.user_id = Guid.Parse(newUser.Id).ToByteArray();
            newInvUser.user_name = newUser.UserName;
            newInvUser.password = "???";
            newInvUser.display_name = newUser.UserName;
            newInvUser.type = accessLevel;
            newInvUser.email = newUser.Email;
            newInvUser.join_date = newUser.JoinDate;
            newInvUser.contact = newUser.PhoneNumber;

            _inventoryAccounts.Users.Add(newInvUser);
            _inventoryAccounts.SaveChanges();

            switch (accessLevel)
            {
                case 1: //User
                    Customers newCustomersEntry = new Customers();
                    newCustomersEntry.customer_id = Guid.NewGuid().ToByteArray();
                    newCustomersEntry.user_id = Guid.Parse(newUser.Id).ToByteArray();
                    newCustomersEntry.times_ordered = 0;

                    await _inventoryAccounts.Customers.AddAsync(newCustomersEntry);
                    break;
                case 2: //Artist
                    Employee newEmployeeEntry = new Employee();
                    newEmployeeEntry.employee_id = Guid.NewGuid().ToByteArray();
                    newEmployeeEntry.user_id = Guid.Parse(newUser.Id).ToByteArray();
                    newEmployeeEntry.employment_date = newUser.JoinDate;

                    await _inventoryAccounts.Employee.AddAsync(newEmployeeEntry);

                    break;
                case 3: // Admin
                    break;
                case 4: //Manager
                    break;
            }

            await _inventoryAccounts.SaveChangesAsync();

            return 1;
        }
        public async Task<int> UpdateUser(APIUsers newUser, PatchUser updateInfo)
        {
            Users? selectedInventoryAccount = null;
            try
            {
                selectedInventoryAccount = await _inventoryAccounts.Users.Where(x => x.email == newUser.Email).FirstAsync();
            }
            catch (Exception ex)
            {
                return 0;
            }

            selectedInventoryAccount.display_name = updateInfo.username;
            selectedInventoryAccount.user_name = updateInfo.username;
            selectedInventoryAccount.contact = updateInfo.phone_number;

            await _inventoryAccounts.Users.AddAsync(selectedInventoryAccount);
            await _inventoryAccounts.SaveChangesAsync();

            return 1;
        }
    }

    public interface IInventoryBOMBridge
    {
        public Task<int> AddCreatedAccountToInventoryAccountTables(APIUsers newUser, int accessLevel);
        public Task<int> UpdateUser(APIUsers newUser, PatchUser updateInfo);
    }
}