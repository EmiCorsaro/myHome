using MyHome.Modules.Ledger.Contracts.Accounts;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Renames an account, changes its type, and archives or unarchives it (story 005).
/// </summary>
public interface IAccountLifecycleRegistrar
{
    Task<AccountSummary> RenameAsync(
        Guid accountId,
        RenameAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountSummary> ChangeTypeAsync(
        Guid accountId,
        ChangeAccountTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountSummary> ArchiveAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<AccountSummary> UnarchiveAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);
}
