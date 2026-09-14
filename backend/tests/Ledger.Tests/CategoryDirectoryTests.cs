using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Domain;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Reading the category tree, as story 007 specifies it. Every test names the requirement it pins
/// down; the map from RF to test lives in <c>progress/impl_007.md</c>.
/// </summary>
public sealed class CategoryDirectoryTests : IDisposable
{
    private const int OtherHouseholdId = HouseholdId + 1;

    private readonly LedgerDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // RF-2
    [Fact(DisplayName = "The income list holds only income categories")]
    public async Task the_income_list_holds_only_income_categories()
    {
        await Existing("Sueldo", CategoryKind.Income);
        await Existing("Supermercado");

        var income = await NewDirectory().ListIncomeCategoriesAsync();

        var only = Assert.Single(income);
        Assert.Equal("Sueldo", only.Name);
        Assert.Equal("income", only.Kind);
    }

    // RF-2
    [Fact(DisplayName = "The expense list holds only expense categories")]
    public async Task the_expense_list_holds_only_expense_categories()
    {
        await Existing("Sueldo", CategoryKind.Income);
        await Existing("Supermercado");

        var expense = await NewDirectory().ListExpenseCategoriesAsync();

        var only = Assert.Single(expense);
        Assert.Equal("Supermercado", only.Name);
        Assert.Equal("expense", only.Kind);
    }

    // RF-1
    [Theory(DisplayName = "A subcategory is listed right after its parent and points at it")]
    [InlineData(CategoryKind.Expense)]
    [InlineData(CategoryKind.Income)]
    public async Task a_subcategory_is_listed_right_after_its_parent_and_points_at_it(
        CategoryKind kind)
    {
        var home = await Existing("Casa", kind);
        await Existing("Ocio", kind);
        var power = await Existing("Luz", kind, parent: home);

        var tree = await List(kind);

        Assert.Equal(["Casa", "Luz", "Ocio"], Names(tree));
        Assert.Null(tree[0].ParentId);
        Assert.Equal(home.PublicId, tree[1].ParentId);
        Assert.Equal(power.PublicId, tree[1].Id);
        Assert.Null(tree[2].ParentId);
    }

    // RF-1, edge case: a parent with no subcategory
    [Fact(DisplayName = "A parent with no subcategory is listed as a top-level category")]
    public async Task a_parent_with_no_subcategory_is_listed_as_a_top_level_category()
    {
        await Existing("Viajes");

        var tree = await NewDirectory().ListExpenseCategoriesAsync();

        var only = Assert.Single(tree);
        Assert.Equal("Viajes", only.Name);
        Assert.Null(only.ParentId);
    }

    // RF-3
    [Fact(DisplayName = "Each category is listed with its colour")]
    public async Task each_category_is_listed_with_its_colour()
    {
        var home = await Existing("Casa", colorIndex: 4);
        await Existing("Luz", parent: home, colorIndex: 9);
        await Existing("Sueldo", CategoryKind.Income, colorIndex: 2);

        var expense = await NewDirectory().ListExpenseCategoriesAsync();
        var income = await NewDirectory().ListIncomeCategoriesAsync();

        Assert.Equal([4, 9], expense.Select(c => c.ColorIndex));
        Assert.Equal([2], income.Select(c => c.ColorIndex));
    }

    // RF-4
    [Theory(DisplayName = "Archived categories are left out by default")]
    [InlineData(CategoryKind.Expense)]
    [InlineData(CategoryKind.Income)]
    public async Task archived_categories_are_left_out_by_default(CategoryKind kind)
    {
        var home = await Existing("Casa", kind);
        await Existing("Luz", kind, parent: home, archived: true);
        await Existing("Antiguo", kind, archived: true);

        var tree = await List(kind);

        var only = Assert.Single(tree);
        Assert.Equal("Casa", only.Name);
        Assert.False(only.IsArchived);
    }

    // RF-5
    [Theory(DisplayName = "Asking for archived categories lists them marked as archived")]
    [InlineData(CategoryKind.Expense)]
    [InlineData(CategoryKind.Income)]
    public async Task asking_for_archived_categories_lists_them_marked_as_archived(
        CategoryKind kind)
    {
        await Existing("Casa", kind);
        await Existing("Antiguo", kind, archived: true);

        var tree = await List(kind, includeArchived: true);

        Assert.Equal(["Antiguo", "Casa"], Names(tree));
        Assert.True(tree[0].IsArchived);
        Assert.False(tree[1].IsArchived);
    }

    // RF-5, edge case: archived parent with its subcategories archived in cascade
    [Fact(DisplayName = "An archived parent and its archived subcategories keep their hierarchy")]
    public async Task an_archived_parent_and_its_archived_subcategories_keep_their_hierarchy()
    {
        var car = await Existing("Coche", archived: true);
        await Existing("Seguro", parent: car, archived: true);
        await Existing("Gasolina", parent: car, archived: true);
        await Existing("Casa");

        var hidden = await NewDirectory().ListExpenseCategoriesAsync();
        var tree = await NewDirectory().ListExpenseCategoriesAsync(includeArchived: true);

        Assert.Equal(["Casa"], Names(hidden));
        Assert.Equal(["Casa", "Coche", "Gasolina", "Seguro"], Names(tree));
        Assert.Equal(car.PublicId, tree[2].ParentId);
        Assert.Equal(car.PublicId, tree[3].ParentId);
        Assert.All(tree.Skip(1), c => Assert.True(c.IsArchived));
    }

    // RF-6
    [Fact(DisplayName = "Top-level categories are listed alphabetically, whatever order they were created or displayed in")]
    public async Task top_level_categories_are_listed_alphabetically()
    {
        await Existing("Viajes", displayOrder: 1);
        await Existing("Casa", displayOrder: 3);
        await Existing("Ocio", displayOrder: 2);

        var tree = await NewDirectory().ListExpenseCategoriesAsync();

        Assert.Equal(["Casa", "Ocio", "Viajes"], Names(tree));
    }

    // RF-6
    [Fact(DisplayName = "Subcategories are listed alphabetically under their own parent")]
    public async Task subcategories_are_listed_alphabetically_under_their_own_parent()
    {
        var travel = await Existing("Viajes");
        var home = await Existing("Casa");
        await Existing("Vuelos", parent: travel);
        await Existing("Luz", parent: home);
        await Existing("Hoteles", parent: travel);
        await Existing("Agua", parent: home);

        var tree = await NewDirectory().ListExpenseCategoriesAsync();

        Assert.Equal(["Casa", "Agua", "Luz", "Viajes", "Hoteles", "Vuelos"], Names(tree));
    }

    // RF-9
    [Fact(DisplayName = "Case is ignored when ordering by name")]
    public async Task case_is_ignored_when_ordering_by_name()
    {
        await Existing("Zapatos");
        await Existing("banco");
        await Existing("Alquiler");

        var tree = await NewDirectory().ListExpenseCategoriesAsync();

        Assert.Equal(["Alquiler", "banco", "Zapatos"], Names(tree));
    }

    // RF-9, RF-10, edge case from the spec
    [Fact(DisplayName = "Names that differ in case or accents at the start sort as the spec says")]
    public async Task names_that_differ_in_case_or_accents_at_the_start_sort_as_the_spec_says()
    {
        await Existing("zapatos");
        await Existing("Banco");
        await Existing("ágil");

        var tree = await NewDirectory().ListExpenseCategoriesAsync();

        Assert.Equal(["ágil", "Banco", "zapatos"], Names(tree));
    }

    // RF-10
    [Fact(DisplayName = "Accents are ignored when ordering subcategories by name")]
    public async Task accents_are_ignored_when_ordering_subcategories_by_name()
    {
        var parent = await Existing("Casa");
        await Existing("Óptica", parent: parent);
        await Existing("Electricidad", parent: parent);
        await Existing("Ñoquis", parent: parent);
        await Existing("Nube", parent: parent);
        await Existing("Pan", parent: parent);

        var tree = await NewDirectory().ListExpenseCategoriesAsync();

        // "Ñoquis" folds to "NOQUIS", so it comes before "Nube": RF-10 treats the tilde of the ñ
        // as a diacritic like any other.
        Assert.Equal(["Casa", "Electricidad", "Ñoquis", "Nube", "Óptica", "Pan"], Names(tree));
    }

    // RF-9, RF-10
    [Theory(DisplayName = "A name is compared without its case or accents")]
    [InlineData("ágil", "AGIL")]
    [InlineData("Ñandú", "NANDU")]
    [InlineData("Pingüino", "PINGUINO")]
    [InlineData("Crème brûlée", "CREME BRULEE")]
    [InlineData("Ação", "ACAO")]
    [InlineData("Ĺuč", "LUC")]
    [InlineData("Café", "CAFE")]
    public void a_name_is_compared_without_its_case_or_accents(string name, string key)
    {
        Assert.Equal(key, CategoryNameOrder.SortKey(name));
    }

    // RF-10
    [Fact(DisplayName = "Every accented letter of the folding table maps to a plain letter")]
    public void every_accented_letter_of_the_folding_table_maps_to_a_plain_letter()
    {
        Assert.Equal(CategoryNameOrder.Accented.Length, CategoryNameOrder.Unaccented.Length);
        Assert.All(CategoryNameOrder.Unaccented, letter => Assert.True(char.IsAsciiLetterUpper(letter)));
        Assert.Equal(
            CategoryNameOrder.Accented.Length,
            CategoryNameOrder.Accented.Distinct().Count());
    }

    // RF-6, RF-10
    [Fact(DisplayName = "Names that fold to the same key still come out in a stable order")]
    public async Task names_that_fold_to_the_same_key_still_come_out_in_a_stable_order()
    {
        await Existing("Agil", CategoryKind.Income);

        var parent = await Existing("Casa", CategoryKind.Income);
        await Existing("Éxito", CategoryKind.Income, parent: parent);
        await Existing("Exito", CategoryKind.Income, parent: parent, archived: true);

        var tree = await NewDirectory().ListIncomeCategoriesAsync(includeArchived: true);

        Assert.Equal(["Agil", "Casa", "Exito", "Éxito"], Names(tree));
    }

    // RF-8
    [Theory(DisplayName = "Only the categories of the caller's household are listed")]
    [InlineData(CategoryKind.Expense, false)]
    [InlineData(CategoryKind.Expense, true)]
    [InlineData(CategoryKind.Income, false)]
    [InlineData(CategoryKind.Income, true)]
    public async Task only_the_categories_of_the_callers_household_are_listed(
        CategoryKind kind,
        bool includeArchived)
    {
        await Existing("Casa", kind);
        var foreign = await Existing("Ajena", kind, householdId: OtherHouseholdId);
        await Existing("Hija ajena", kind, householdId: OtherHouseholdId, parent: foreign);
        await Existing("Archivada ajena", kind, householdId: OtherHouseholdId, archived: true);

        var tree = await List(kind, includeArchived);

        Assert.Equal(["Casa"], Names(tree));
    }

    // RF-7 (server side): an empty household gets an empty list, which the screen shows as empty
    [Fact(DisplayName = "A household with no categories gets empty lists")]
    public async Task a_household_with_no_categories_gets_empty_lists()
    {
        await Existing("Ajena", householdId: OtherHouseholdId);
        await Existing("Ajena de ingreso", CategoryKind.Income, householdId: OtherHouseholdId);

        Assert.Empty(await NewDirectory().ListExpenseCategoriesAsync());
        Assert.Empty(await NewDirectory().ListIncomeCategoriesAsync());
        Assert.Empty(await NewDirectory().ListExpenseCategoriesAsync(includeArchived: true));
        Assert.Empty(await NewDirectory().ListIncomeCategoriesAsync(includeArchived: true));
    }

    // RF-1, RF-4: guard against rows that break cascading archival
    [Fact(DisplayName = "An active subcategory of an archived parent is not lost from the list")]
    public async Task an_active_subcategory_of_an_archived_parent_is_not_lost_from_the_list()
    {
        var car = await Existing("Coche", archived: true);
        await Existing("Seguro", parent: car);
        await Existing("Casa");

        var tree = await NewDirectory().ListExpenseCategoriesAsync();

        Assert.Equal(["Casa", "Seguro"], Names(tree));
        Assert.Equal(car.PublicId, tree[1].ParentId);
    }

    private static string[] Names(IEnumerable<CategorySummary> tree) =>
        [.. tree.Select(c => c.Name)];

    private CategoryDirectory NewDirectory() =>
        new(_database.Context, new TestTenantContext(HouseholdId));

    private Task<IReadOnlyList<CategorySummary>> List(
        CategoryKind kind,
        bool includeArchived = false) =>
        kind == CategoryKind.Income
            ? NewDirectory().ListIncomeCategoriesAsync(includeArchived)
            : NewDirectory().ListExpenseCategoriesAsync(includeArchived);

    private async Task<Category> Existing(
        string name,
        CategoryKind kind = CategoryKind.Expense,
        int householdId = HouseholdId,
        Category? parent = null,
        bool archived = false,
        int colorIndex = 1,
        int displayOrder = 0)
    {
        var category = Category.Create(
            householdId,
            name,
            kind,
            colorIndex,
            displayOrder,
            parentId: parent?.Id);

        if (archived)
        {
            category.Archive();
        }

        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        return category;
    }
}
