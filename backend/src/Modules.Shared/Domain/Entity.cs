namespace MyHome.Modules.Shared.Domain;

public abstract class Entity
{
    protected Entity()
    {
    }

    public int Id { get; protected set; }
}
