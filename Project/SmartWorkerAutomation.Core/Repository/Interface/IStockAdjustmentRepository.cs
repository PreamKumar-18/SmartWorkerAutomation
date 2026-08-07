using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IStockAdjustmentRepository
{
    void Insert(StockAdjustment stockAdjustment);
    Task<List<StockAdjustment>> GetPaginatedStockAdjustmentsAsync(Paging pagingConfig, int itemId);
}
