using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SmartWorkerAutomation.Core.Repository.Automation;
using System.Data;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public class DbConnectionFactory
{
    private readonly string _connectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantResolverService _tenantResolver;
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

    public DbConnectionFactory(IHttpContextAccessor httpContextAccessor, ITenantResolverService tenantResolver)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantResolver = tenantResolver;
    }

    public IDbConnection CreateConnection()
    {
        var connectionString = ResolveConnectionStringAsync().GetAwaiter().GetResult();
        return new NpgsqlConnection(connectionString);
    }
    private async Task<string> ResolveConnectionStringAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "DbConnectionFactory.CreateConnection called outside an HTTP request context - " +
                "background services must resolve their own tenant connection explicitly, not via this factory.");

        var orgIdClaim = httpContext.User.FindFirst("orgid")?.Value
            ?? throw new InvalidOperationException(
                "No 'orgid' claim on the current user - request is unauthenticated or the token predates multi-tenancy.");

        if (!int.TryParse(orgIdClaim, out var orgId))
        {
            throw new InvalidOperationException($"'orgid' claim value '{orgIdClaim}' is not a valid integer.");
        }

        var connectionString = await _tenantResolver.GetTenantConnectionStringAsync(orgId);
        return connectionString
            ?? throw new InvalidOperationException($"Could not resolve a connection string for orgid {orgId}.");
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
