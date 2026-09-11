namespace MyHome.Modules.Ledger.Contracts.Movements;

/// <summary>
/// What a member is told once a movement has been voided (story 013).
/// </summary>
/// <param name="MovementId">Public identifier of the movement that was voided.</param>
/// <param name="ReversalMovementId">
/// Public identifier of the reversal that was registered against it (RF-1, RF-6). Together with
/// <paramref name="MovementId"/> this is the link the story asks for: readable from either side
/// without the caller having to search by date or amount.
/// </param>
/// <param name="OccurredOn">
/// Date the reversal was recorded at; the same date the voided movement carried (RF-11).
/// </param>
/// <param name="MovementKind">
/// The kind of movement that was voided: <c>"expense"</c>, <c>"income"</c> or <c>"transfer"</c>
/// (RF-10).
/// </param>
public sealed record VoidedMovement(
    Guid MovementId,
    Guid ReversalMovementId,
    DateOnly OccurredOn,
    string MovementKind);
