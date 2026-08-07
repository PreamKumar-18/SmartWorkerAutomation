using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Document> _repository;

    public DocumentRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Document>(dbContext);
    }

    public async Task<List<Document>> GetDocumentsByReferenceAsync(int categoryId, int referenceId)
    {
        var data = await _repository.SearchAsync( x => x.CategoryId == categoryId && x.ReferenceId == referenceId && x.IsActive);
        return data.ToList();
    }

    public void Insert(Document entity)
    {
        _repository.Insert(entity);
    }

    public void Update(Document entity)
    {
        _repository.Update(entity);
    }

    public void Delete(Document entity)
    {
        _repository.Delete(entity);
    }

    public async Task<Document> GetDocumentByIdAsync(int documentId)
    {
        return await _repository.GetByIdAsync(documentId);
    }
}
