using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public class MasterDbConnectionFactory
{
    private readonly string _connectionString;

    public MasterDbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MasterConnection")
            ?? throw new InvalidOperationException("Connection string 'MasterConnection' not found.");
    }

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
