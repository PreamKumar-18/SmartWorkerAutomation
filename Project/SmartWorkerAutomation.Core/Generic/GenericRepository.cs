// Unused, dead - this was the old EF Core-based generic repository from
// before the switch to Dapper + IQueryStore/Queries.json (see
// SmartWorkerAutomation.Core.Repository.Automation.GenericRepository for the
// real, actually-registered implementation). It was already fully
// commented-out with no live code, and its DI registration in
// ConfigureRepositoryServiceExtension.cs is commented out too - nothing in
// the app references SmartWorkerAutomation.Core.Generic.GenericRepository<T>.
// Content intentionally left empty rather than the file being deleted - this
// workspace doesn't allow deleting files under source control here; it's
// safe to delete this file manually.
