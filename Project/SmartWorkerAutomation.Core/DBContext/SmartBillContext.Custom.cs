using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SmartWorkerAutomation.Core.Interface;

namespace SmartWorkerAutomation.Core.DBContext;

public partial class SmartWorkerAutomationContext
{
    public override int SaveChanges()
    {
        ProcessAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProcessAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ProcessAuditFields()
    {
        var currentUserService = this.GetService<ICurrentUserService>();
        if (currentUserService == null) return;

        var userId = currentUserService.GetCurrentUserIdNullable();
        if (userId == null) return;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                var createdByProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "CreatedBy");
                if (createdByProp != null && (createdByProp.CurrentValue == null || createdByProp.CurrentValue.Equals(0)))
                {
                    createdByProp.CurrentValue = userId.Value;
                }

                var updatedByProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedBy");
                if (updatedByProp != null && (updatedByProp.CurrentValue == null || updatedByProp.CurrentValue.Equals(0)))
                {
                    updatedByProp.CurrentValue = userId.Value;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                var updatedByProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedBy");
                if (updatedByProp != null)
                {
                    updatedByProp.CurrentValue = userId.Value;
                }
            }
        }
    }
}
