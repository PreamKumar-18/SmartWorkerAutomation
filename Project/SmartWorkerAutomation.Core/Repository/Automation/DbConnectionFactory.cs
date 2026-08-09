using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public class DbConnectionFactory
{
    private readonly string _connectionString;

    static DbConnectionFactory()
    {
        // Dapper 2.1.79 has no built-in DbType mapping for System.DateOnly -
        // SqlMapper.LookupDbType throws NotSupportedException for any query
        // parameter typed as DateOnly (or DateOnly?, since Dapper unwraps
        // Nullable<T> before the type-map lookup). Hit in production via
        // ReplyClassificationResult.PromisedDate (DateOnly?) passed straight
        // into ReplyClassification:UpsertIntent's parameter object. Registering
        // this handler once, here, covers every current and future Dapper call
        // site that passes a DateOnly parameter, instead of patching each one.
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartWorkerConnection")
            ?? throw new InvalidOperationException("Connection string 'SmartWorkerConnection' not found.");
    }

    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }

        public override DateOnly Parse(object value) => value switch
        {
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            _ => DateOnly.Parse(value.ToString()!),
        };
    }
}
