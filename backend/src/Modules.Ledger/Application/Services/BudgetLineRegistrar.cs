using FluentValidation;
using MyHome.Modules.Ledger.Contracts.Budget;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Ledger.Persistence;
using MyHome.Modules.Shared.Contracts;
using MyHome.Modules.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MyHome.Modules.Ledger.Application;

/// <summary>
/// Declaring a budget line, as stories 051 and 052 specify it.
/// </summary>
/// <remarks>
/// No role is consulted anywhere in here, deliberately: any member of the household declares, and
/// asking for a title would be a rule nobody wrote down (criterion C2).
/// </remarks>
internal sealed class BudgetLineRegistrar(
    LedgerDbContext db,
    ITenantContext tenant,
    IValidator<DeclareBudgetLineRequest> validator) : IBudgetLineRegistrar
{
    public async Task<BudgetLineSummary> DeclareAsync(
        DeclareBudgetLineRequest request,
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
        var month = new DateOnly(request.Month.Year, request.Month.Month, 1);
        var sign = ToSign(request.Sign);

        var category = await db.Categories
            .FirstOrDefaultAsync(
                c => c.PublicId == request.CategoryId
                    && c.HouseholdId == householdId
                    && !c.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("categoryId", "Esa categoría no está disponible.");

        var account = await db.Accounts
            .FirstOrDefaultAsync(
                a => a.PublicId == request.AccountId
                    && a.HouseholdId == householdId
                    && !a.IsArchived,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw Invalid("accountId", "Esa cuenta no está disponible.");

        if (category.Kind != BudgetLine.KindOf(sign))
        {
            // The message names what the category actually classifies, which depends on the sign
            // the caller declared. Fixed wording would tell an income line that its expense
            // category classifies income: the exact opposite of what happened.
            throw Invalid("categoryId", MismatchedCategoryMessage(sign));
        }

        var alreadyDeclared = await db.BudgetLines
            .AnyAsync(
                b => b.HouseholdId == householdId
                    && b.CategoryId == category.Id
                    && b.PeriodStart == month,
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyDeclared)
        {
            throw Invalid("categoryId", DuplicateLineMessage);
        }

        var line = BudgetLine.Declare(
            householdId,
            sign,
            category,
            account,
            month,
            request.Amount,
            ToAmountMode(request.AmountMode),
            ToOrigin(request.Origin));

        db.BudgetLines.Add(line);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // The row was refused, so it is not there: stop tracking it before asking the context
            // anything else.
            db.Entry(line).State = EntityState.Detached;

            // Lost the race: the other member of the household declared the same category and
            // month between the check above and this insert, and the unique index refused the row.
            // The caller gets the same error it would have got a millisecond earlier. Any other
            // failed insert is not ours to explain and travels on.
            if (await WasDeclaredMeanwhileAsync(householdId, category.Id, month, cancellationToken)
                .ConfigureAwait(false))
            {
                throw Invalid("categoryId", DuplicateLineMessage);
            }

            throw;
        }

        return BudgetDirectory.ToSummary(line, category, account);
    }

    /// <summary>Tells the lost race apart from any other failed insert.</summary>
    /// <param name="householdId">Household the line would have belonged to.</param>
    /// <param name="categoryId">Category it was declared against.</param>
    /// <param name="month">First day of the month it covered.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><c>true</c> when the line exists now, after the insert was refused.</returns>
    private async Task<bool> WasDeclaredMeanwhileAsync(
        int householdId,
        int categoryId,
        DateOnly month,
        CancellationToken cancellationToken) =>
        await db.BudgetLines
            .AsNoTracking()
            .AnyAsync(
                b => b.HouseholdId == householdId
                    && b.CategoryId == categoryId
                    && b.PeriodStart == month,
                cancellationToken)
            .ConfigureAwait(false);

    private const string DuplicateLineMessage =
        "Esa categoría ya tiene una línea de presupuesto en ese mes.";

    /// <summary>Says what the chosen category classifies, given what the line declares.</summary>
    /// <param name="sign">Sign the caller declared.</param>
    /// <returns>The message for a category of the other kind.</returns>
    private static string MismatchedCategoryMessage(BudgetLineSign sign) =>
        sign == BudgetLineSign.Income
            ? "Esa categoría clasifica gastos, no ingresos."
            : "Esa categoría clasifica ingresos, no gastos.";

    private static BudgetLineSign ToSign(string sign) => sign switch
    {
        BudgetLineSigns.Expense => BudgetLineSign.Expense,
        BudgetLineSigns.Income => BudgetLineSign.Income,
        _ => throw new ArgumentOutOfRangeException(nameof(sign), sign, "Unknown budget line sign."),
    };

    private static BudgetIncomeOrigin? ToOrigin(string? origin) => origin switch
    {
        null or "" => null,
        BudgetIncomeOrigins.Payroll => BudgetIncomeOrigin.Payroll,
        BudgetIncomeOrigins.SelfEmployment => BudgetIncomeOrigin.SelfEmployment,
        BudgetIncomeOrigins.Rental => BudgetIncomeOrigin.Rental,
        BudgetIncomeOrigins.Investment => BudgetIncomeOrigin.Investment,
        BudgetIncomeOrigins.Benefit => BudgetIncomeOrigin.Benefit,
        BudgetIncomeOrigins.Refund => BudgetIncomeOrigin.Refund,
        BudgetIncomeOrigins.Gift => BudgetIncomeOrigin.Gift,
        BudgetIncomeOrigins.Other => BudgetIncomeOrigin.Other,
        _ => throw new ArgumentOutOfRangeException(
            nameof(origin), origin, "Unknown budget income origin."),
    };

    private static PlannedAmountMode ToAmountMode(string mode) => mode switch
    {
        BudgetAmountModes.Fixed => PlannedAmountMode.Fixed,
        BudgetAmountModes.Estimated => PlannedAmountMode.Estimated,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown amount mode."),
    };

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
