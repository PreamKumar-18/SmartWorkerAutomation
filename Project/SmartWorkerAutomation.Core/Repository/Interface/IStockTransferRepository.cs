using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IStockTransferRepository
{
    void Insert(StockTransfer stockTransfer);
    Task<List<StockTransfer>> GetPaginatedStockTransferAsync(Paging pagingConfig,int itemId);
}
