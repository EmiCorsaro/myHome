using MyHome.Modules.Ledger.Contracts.Movements;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Voids a movement already registered — expense, income or transfer alike (story 013, RF-10).
/// </summary>
public interface IMovementLifecycleRegistrar
{
    Task<VoidedMovement> VoidAsync(
        Guid movementId,
        CancellationToken cancellationToken = default);
}
