using SmartWorkerAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IStockAdjustmentItemRepository
{
    void Insert(StockAdjustmentItem stockAdjustmentItem);

    Task InsertManyAsync(List<StockAdjustmentItem> items);
}
