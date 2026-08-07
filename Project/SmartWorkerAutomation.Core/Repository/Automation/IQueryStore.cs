namespace SmartWorkerAutomation.Core.Repository.Automation;

/// <summary>
/// Central source of truth for every hand-written SQL statement the API
/// runs. Backed by Config/Queries.json (see SmartWorker/Config/Queries.json)
/// via the standard IConfiguration pipeline - the same mechanism
/// DbConnectionFactory already uses for the connection string - so a query
/// lookup is just "Section:Key" the way appsettings.json values already are.
///
/// Queries.json is loaded with reloadOnChange: true (see Program.cs), so
/// editing a query is a config change, not a recompile/redeploy.
/// </summary>
public interface IQueryStore
{
    /// <summary>
    /// Returns the raw SQL text for the given "Section:Key" path (e.g.
    /// "ReplyReview:GetQueue"). Throws InvalidOperationException with the
    /// missing key's full path if it isn't in Queries.json - fails loudly at
    /// the call site rather than silently running a null/empty command.
    /// </summary>
    string Get(string key);

    /// <summary>
    /// Same as <see cref="Get"/>, but first substitutes "{Token}" placeholders
    /// in the query text with the supplied values. This is only for the
    /// structural pieces of a query that can't be a Dapper @param because
    /// they're part of the SQL shape itself (a view/table name, an optional
    /// WHERE/AND fragment that may be blank, an ORDER BY column) - never for
    /// user-supplied values, which must stay real Dapper parameters.
    /// </summary>
    string Render(string key, IReadOnlyDictionary<string, string> tokens);
}
