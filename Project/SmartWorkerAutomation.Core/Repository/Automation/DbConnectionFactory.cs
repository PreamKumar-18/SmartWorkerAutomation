using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartWorkerConnection") 
            ?? throw new InvalidOperationException("Connection string 'SmartWorkerConnection' not found.");
    }

    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
