using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient; // Adjust according to your database provider
using Microsoft.Extensions.Configuration; // For accessing configuration settings
using BOM_API_v2.Services;

namespace BOM_API_v2.Data
{
    public class DataAccess : IDataAccess
    {
        private readonly string _connectionString;

        public DataAccess(IConfiguration configuration)
        {
            // Assuming you have a connection string in your appsettings.json
            _connectionString = configuration.GetConnectionString("connection");
        }

        public async Task<DateTime?> GetPickupDateAsync(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"SELECT pickup_date FROM orders WHERE order_id = @orderId";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    var result = await command.ExecuteScalarAsync();
                    return result != null ? (DateTime?)Convert.ToDateTime(result) : null;
                }
            }
        }
    }
}
