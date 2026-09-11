namespace MyHome.Modules.Shared.Domain;

public abstract class AuditedTenantEntity : TenantEntity
{
    protected AuditedTenantEntity()
    {
    }

    public DateTimeOffset CreatedAt { get; protected set; }
}
