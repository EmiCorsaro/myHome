using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Expenses;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Application;
using MyHome.Modules.Shared.Domain;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

internal sealed class ExpenseRegistrar(
    LedgerDbContext db,
    ITenantContext tenant,
    IValidator<RegisterExpenseRequest> validator,
    IHouseholdDirectory households) : IExpenseRegistrar
{
    public async Task<RegisteredExpense> RegisterAsync(
        RegisterExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await validator.ValidateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!validation.IsValid)
        {
            throw new ValidationFailedException(
                validation.Errors
                    .GroupBy(e => ToFieldName(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var householdId = tenant.RequireHouseholdId();

        if (request.ClientMutationId is { Length: > 0 } key)
        {
            var already = await FindByMutationKeyAsync(householdId, key, cancellationToken)
                .ConfigureAwait(false);

            if (already is not null)
            {
                return already;
            }
        }

        var account = await db.Accounts
            .FirstOrDefaultAsync(
                a => a.PublicId == request.AccountId && a.HouseholdId == householdId && !a.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("accountId", "That account is not available.");

        var category = await db.Categories
            .FirstOrDefaultAsync(
                c => c.PublicId == request.CategoryId && c.HouseholdId == householdId && !c.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("categoryId", "That category is not available.");

        int? memberId = null;

        if (request.MemberId is { } memberPublicId)
        {
            memberId = await households
                .ResolveMemberAsync(memberPublicId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw Invalid("memberId", "That member is not in this household.");
        }

        var expenseAccount = await GetOrCreateExpenseAccountAsync(
                householdId,
                account.Currency,
                cancellationToken)
            .ConfigureAwait(false);

        RecurringRule? rule = null;

        if (request.Recurrence != ExpenseRecurrence.Once)
        {
            rule = RecurringRule.Create(
                householdId,
                EntryKind.Expense,
                ToFrequency(request.Recurrence),
                account,
                category,
                request.Description,
                Money.Of(request.Amount, account.Currency),
                request.OccurredOn);

            db.RecurringRules.Add(rule);
        }

        var entry = JournalEntry.RegisterExpense(
            householdId,
            request.OccurredOn,
            request.Description,
            paidFrom: account,
            expenseAccount: expenseAccount,
            category: category,
            amount: Money.Of(request.Amount, account.Currency),
            memberId: memberId,
            clientMutationId: request.ClientMutationId,
            recurringRule: rule);

        db.Entries.Add(entry);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (request.ClientMutationId is { Length: > 0 } raced)
        {
            db.ChangeTracker.Clear();

            var recovered = await FindByMutationKeyAsync(householdId, raced, cancellationToken)
                .ConfigureAwait(false);

            if (recovered is null)
            {
                throw;
            }

            return recovered;
        }

        return Describe(entry, account, category, request.Recurrence, wasAlreadyRegistered: false);
    }

    private async Task<RegisteredExpense?> FindByMutationKeyAsync(
        int householdId,
        string key,
        CancellationToken cancellationToken)
    {
        var entry = await db.Entries
            .Include(e => e.Postings)
            .FirstOrDefaultAsync(
                e => e.HouseholdId == householdId && e.ClientMutationId == key,
                cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return null;
        }

        var accountId = entry.Postings.First(p => p.CategoryId is null).AccountId;
        var categoryId = entry.Postings.First(p => p.CategoryId is not null).CategoryId;

        var account = await db.Accounts
            .FirstAsync(a => a.Id == accountId, cancellationToken)
            .ConfigureAwait(false);

        var category = await db.Categories
            .FirstAsync(c => c.Id == categoryId, cancellationToken)
            .ConfigureAwait(false);

        var recurrence = entry.RecurringRuleId is null
            ? ExpenseRecurrence.Once
            : ToRecurrence(await db.RecurringRules
                .Where(r => r.Id == entry.RecurringRuleId)
                .Select(r => r.Frequency)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false));

        return Describe(entry, account, category, recurrence, wasAlreadyRegistered: true);
    }

    private async Task<Account> GetOrCreateExpenseAccountAsync(
        int householdId,
        CurrencyCode currency,
        CancellationToken cancellationToken)
    {
        var existing = await db.Accounts
            .FirstOrDefaultAsync(
                a => a.HouseholdId == householdId
                    && a.Type == AccountType.Expense
                    && a.Currency == currency,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var created = Account.Create(
            householdId,
            "Expenses",
            AccountType.Expense,
            currency,
            displayOrder: 900);

        db.Accounts.Add(created);

        return created;
    }

    private static RegisteredExpense Describe(
        JournalEntry entry,
        Account account,
        Category category,
        ExpenseRecurrence recurrence,
        bool wasAlreadyRegistered)
    {
        var amount = entry.Postings
            .Where(p => p.CategoryId is not null)
            .Sum(p => p.Amount);

        return new RegisteredExpense(
            entry.PublicId,
            entry.OccurredOn,
            entry.Description,
            decimal.Round(amount, 2, MidpointRounding.ToEven),
            account.Currency.Value,
            account.Name,
            category.Name,
            category.ColorIndex,
            recurrence,
            wasAlreadyRegistered);
    }

    private static RecurrenceFrequency ToFrequency(ExpenseRecurrence recurrence) => recurrence switch
    {
        ExpenseRecurrence.Monthly => RecurrenceFrequency.Monthly,
        ExpenseRecurrence.BiMonthly => RecurrenceFrequency.BiMonthly,
        ExpenseRecurrence.Quarterly => RecurrenceFrequency.Quarterly,
        ExpenseRecurrence.Once => throw new ArgumentOutOfRangeException(
            nameof(recurrence),
            recurrence,
            "A one-off expense creates no recurring rule."),
        _ => throw new ArgumentOutOfRangeException(
            nameof(recurrence),
            recurrence,
            "Unknown recurrence."),
    };

    private static ExpenseRecurrence ToRecurrence(RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Monthly => ExpenseRecurrence.Monthly,
        RecurrenceFrequency.BiMonthly => ExpenseRecurrence.BiMonthly,
        RecurrenceFrequency.Quarterly => ExpenseRecurrence.Quarterly,
        _ => throw new ArgumentOutOfRangeException(
            nameof(frequency),
            frequency,
            "The rule has a frequency the contract does not publish."),
    };

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
