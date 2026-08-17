using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public class CustomerEnquiryService : ICustomerEnquiryService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    public CustomerEnquiryService(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<IReadOnlyList<CustomerEnquiry>> ListAsync(CustomerEnquiryListFilter filter)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = _queryStore.Get("CustomerEnquiry:List");
        var rows = await connection.QueryAsync<CustomerEnquiry>(sql, new
        {
            EnquiryStatus = filter.EnquiryStatus,
            IsActive = filter.IsActive,
            Search = filter.Search
        });

        return rows.ToList();
    }

    public async Task<CustomerEnquiry?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("CustomerEnquiry:GetById");
        return await connection.QuerySingleOrDefaultAsync<CustomerEnquiry>(sql, new { Id = id });
    }

    public async Task<CustomerEnquiry> CreateAsync(CreateCustomerEnquiryRequest request, string? createdBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("CustomerEnquiry:Insert");
        return await connection.QuerySingleAsync<CustomerEnquiry>(sql, new
        {
            request.ContactName,
            request.CustomerName,
            request.MailingStreet,
            request.MailingCity,
            request.MailingState,
            request.MailingZip,
            request.Phone,
            request.Email,
            request.EnquiryStatus,
            request.Remarks,
            CreatedBy = createdBy
        });
    }

    public async Task<CustomerEnquiry?> UpdateAsync(UpdateCustomerEnquiryRequest request, string? updatedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("CustomerEnquiry:Update");
        return await connection.QuerySingleOrDefaultAsync<CustomerEnquiry>(sql, new
        {
            request.Id,
            request.ContactName,
            request.CustomerName,
            request.MailingStreet,
            request.MailingCity,
            request.MailingState,
            request.MailingZip,
            request.Phone,
            request.Email,
            request.EnquiryStatus,
            request.Remarks,
            UpdatedBy = updatedBy
        });
    }

    public async Task<CustomerEnquiry?> SetActiveAsync(int id, bool isActive, string? updatedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("CustomerEnquiry:SetActive");
        return await connection.QuerySingleOrDefaultAsync<CustomerEnquiry>(sql, new
        {
            Id = id,
            IsActive = isActive,
            UpdatedBy = updatedBy
        });
    }
}
