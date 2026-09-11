namespace MyHome.Modules.Shared.Domain;

public abstract class TenantEntity : Entity
{
    protected TenantEntity()
    {
    }

    public Guid PublicId { get; protected set; }

    public int HouseholdId { get; protected set; }
}
