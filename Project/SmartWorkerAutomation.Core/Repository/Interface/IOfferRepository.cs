using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IOfferRepository
{
    Task<Offer> GetByIdAsync(int id);
    Task<IEnumerable<Offer>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Insert(Offer entity);
    void Update(Offer entity);
    Task<Offer> GetOfferWithItemsAsync(int offerId);
    Task<List<Offer>> GetActiveOffersAsync(int branchId, System.DateTime billDate, System.TimeSpan parsedBillTime, decimal billTotal);
}
