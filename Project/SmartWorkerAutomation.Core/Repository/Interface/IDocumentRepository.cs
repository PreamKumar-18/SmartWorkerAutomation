using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IDocumentRepository
{
    Task<List<Document>> GetDocumentsByReferenceAsync(int categoryId, int referenceId);
    void Insert(Document entity);
    void Update(Document entity);
    void Delete(Document entity);
    Task<Document> GetDocumentByIdAsync(int documentId);
}
