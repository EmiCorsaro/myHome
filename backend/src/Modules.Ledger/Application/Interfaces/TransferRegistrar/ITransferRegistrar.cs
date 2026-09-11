using MyHome.Modules.Ledger.Contracts.Transfers;

namespace MyHome.Modules.Ledger.Application;

public interface ITransferRegistrar
{
    Task<RegisteredTransfer> RegisterAsync(
        RegisterTransferRequest request,
        CancellationToken cancellationToken = default);
}
