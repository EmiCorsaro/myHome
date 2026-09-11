using MyHome.Modules.Ledger.Contracts.Accounts;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Marks an account as controlled or not, and fixes or retires its minimum buffer (story 004).
/// </summary>
public interface IAccountControlRegistrar
{
    Task<AccountSummary> SetTrackedAsync(
        Guid accountId,
        SetAccountTrackedRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountSummary> SetMinimumBufferTargetAsync(
        Guid accountId,
        SetMinimumBufferTargetRequest request,
        CancellationToken cancellationToken = default);
}
