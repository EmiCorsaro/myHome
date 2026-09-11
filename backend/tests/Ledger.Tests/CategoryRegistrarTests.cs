using Microsoft.EntityFrameworkCore;
using MyHome.Modules.Ledger.Application;
using MyHome.Modules.Ledger.Contracts.Categories;
using MyHome.Modules.Ledger.Domain;
using MyHome.Modules.Shared.Contracts;
using static MyHome.Ledger.Tests.LedgerFixtures;

namespace MyHome.Ledger.Tests;

/// <summary>
/// Creating a category, as story 001 specifies it. Every test names the requirement it pins down;
/// the map from RF to test lives in <c>progress/impl_001.md</c>.
/// </summary>
public sealed class CategoryRegistrarTests : IDisposable
{
    private const int OtherHouseholdId = HouseholdId + 1;

    private readonly LedgerDatabase _database = new();
    private readonly CategoryRegistrar _registrar;

    public CategoryRegistrarTests()
    {
        _registrar = NewRegistrar();
    }

    public void Dispose() => _database.Dispose();

    // RF-1
    [Fact(DisplayName = "A new category belongs to the household that asked for it")]
    public async Task a_new_category_belongs_to_the_household_that_asked_for_it()
    {
        var created = await _registrar.CreateAsync(New("Ocio"));

        var stored = await _database.Context.Categories.SingleAsync(c => c.PublicId == created.Id);

        Assert.Equal(HouseholdId, stored.HouseholdId);
        Assert.Equal("Ocio", stored.Name);
        Assert.Equal(CategoryKind.Expense, stored.Kind);
        Assert.Equal("Ocio", created.Name);
        Assert.Equal("expense", created.Kind);
    }

    // RF-1
    [Fact(DisplayName = "An income category is created as income")]
    public async Task an_income_category_is_created_as_income()
    {
        var created = await _registrar.CreateAsync(New("Sueldo", kind: "income"));

        var stored = await _database.Context.Categories.SingleAsync(c => c.PublicId == created.Id);

        Assert.Equal(CategoryKind.Income, stored.Kind);
        Assert.Equal("income", created.Kind);
    }

    // RF-1, edge case: outer spaces
    [Fact(DisplayName = "A name is stored without its outer spaces")]
    public async Task a_name_is_stored_without_its_outer_spaces()
    {
        var created = await _registrar.CreateAsync(New("  Ocio  "));

        Assert.Equal("Ocio", created.Name);
        Assert.Equal("Ocio", (await _database.Context.Categories.SingleAsync()).Name);
    }

    // RF-2
    [Fact(DisplayName = "A new category is given a colour of the palette, unasked")]
    public async Task a_new_category_is_given_a_colour_of_the_palette_unasked()
    {
        var created = await _registrar.CreateAsync(New("Ocio"));

        Assert.InRange(created.ColorIndex, 1, Category.PaletteSize);
    }

    // RF-2
    [Fact(DisplayName = "The colour does not depend on how many categories the household has")]
    public async Task the_colour_does_not_depend_on_how_many_categories_the_household_has()
    {
        await Existing("Filler 1");
        await Existing("Filler 2", archived: true);
        await Existing("Filler 3");

        var created = await _registrar.CreateAsync(New("Ocio"));

        // Same name, empty household elsewhere: same colour. The old count-and-add-one gave the
        // colour a different value depending on rows the user cannot even see.
        using var elsewhere = new LedgerDatabase();
        var alone = await NewRegistrar(elsewhere).CreateAsync(New("Ocio"));

        Assert.Equal(alone.ColorIndex, created.ColorIndex);
    }

    // RF-2
    [Fact(DisplayName = "Every colour handed out stays inside the palette")]
    public async Task every_colour_handed_out_stays_inside_the_palette()
    {
        for (var i = 0; i < Category.PaletteSize * 3; i++)
        {
            var created = await _registrar.CreateAsync(
                New(string.Create(null, $"Categoría {i}")));

            Assert.InRange(created.ColorIndex, 1, Category.PaletteSize);
        }
    }

    // RF-3
    [Fact(DisplayName = "A category created under a parent becomes its subcategory")]
    public async Task a_category_created_under_a_parent_becomes_its_subcategory()
    {
        var parent = await Existing("Casa");

        var created = await _registrar.CreateAsync(New("Luz", parentId: parent.PublicId));

        var stored = await _database.Context.Categories.SingleAsync(c => c.PublicId == created.Id);

        Assert.Equal(parent.Id, stored.ParentId);
        Assert.Equal(parent.PublicId, created.ParentId);
    }

    // RF-4
    [Fact(DisplayName = "A category created without a parent sits at the top level")]
    public async Task a_category_created_without_a_parent_sits_at_the_top_level()
    {
        var created = await _registrar.CreateAsync(New("Casa"));

        var stored = await _database.Context.Categories.SingleAsync(c => c.PublicId == created.Id);

        Assert.Null(stored.ParentId);
        Assert.Null(created.ParentId);
    }

    // RF-5
    [Theory(DisplayName = "A name already used in the household is refused")]
    [InlineData("Ocio")]
    [InlineData("OCIO")]
    [InlineData("ocio")]
    [InlineData("  Ocio  ")]
    public async Task a_name_already_used_in_the_household_is_refused(string attempted)
    {
        await Existing("Ocio");

        var error = await Rejected(New(attempted));

        Assert.Equal("Ya existe una categoría con ese nombre.", Assert.Single(error.Errors["name"]));
        Assert.Equal(1, await _database.Context.Categories.CountAsync());
    }

    // RF-5
    [Fact(DisplayName = "A name used by a category of the other kind is taken all the same")]
    public async Task a_name_used_by_a_category_of_the_other_kind_is_taken_all_the_same()
    {
        await Existing("Alquiler", CategoryKind.Income);

        var error = await Rejected(New("Alquiler", kind: "expense"));

        Assert.True(error.Errors.ContainsKey("name"));
    }

    // RF-5, edge case: archived category
    [Fact(DisplayName = "An archived category keeps its name taken")]
    public async Task an_archived_category_keeps_its_name_taken()
    {
        await Existing("Ocio", archived: true);

        var error = await Rejected(New("Ocio"));

        Assert.True(error.Errors.ContainsKey("name"));
    }

    // RF-5
    [Fact(DisplayName = "A name used by another household is still free")]
    public async Task a_name_used_by_another_household_is_still_free()
    {
        await Existing("Ocio", householdId: OtherHouseholdId);

        var created = await _registrar.CreateAsync(New("Ocio"));

        Assert.Equal("Ocio", created.Name);
    }

    // RF-6
    [Theory(DisplayName = "A name that is blank is refused")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task a_name_that_is_blank_is_refused(string blank)
    {
        var error = await Rejected(New(blank));

        Assert.Equal(
            "Escribe un nombre para la categoría.",
            Assert.Single(error.Errors["name"]));
        Assert.Empty(_database.Context.Categories);
    }

    // RF-7
    [Fact(DisplayName = "A parent that does not exist is refused")]
    public async Task a_parent_that_does_not_exist_is_refused()
    {
        var error = await Rejected(New("Luz", parentId: Guid.CreateVersion7()));

        Assert.Equal(
            "Esa categoría padre no está disponible.",
            Assert.Single(error.Errors["parentId"]));
        Assert.Empty(_database.Context.Categories);
    }

    // RF-7
    [Fact(DisplayName = "A parent belonging to another household is refused")]
    public async Task a_parent_belonging_to_another_household_is_refused()
    {
        var foreign = await Existing("Casa", householdId: OtherHouseholdId);

        var error = await Rejected(New("Luz", parentId: foreign.PublicId));

        Assert.True(error.Errors.ContainsKey("parentId"));
        Assert.Equal(1, await _database.Context.Categories.CountAsync());
    }

    // RF-8
    [Fact(DisplayName = "An archived parent is refused")]
    public async Task an_archived_parent_is_refused()
    {
        var archived = await Existing("Casa", archived: true);

        var error = await Rejected(New("Luz", parentId: archived.PublicId));

        Assert.True(error.Errors.ContainsKey("parentId"));
    }

    // RF-9
    [Fact(DisplayName = "Any member of the household may create a category")]
    public async Task any_member_of_the_household_may_create_a_category()
    {
        var first = NewRegistrar(memberId: 7);
        var second = NewRegistrar(memberId: 42);
        var anonymous = NewRegistrar(memberId: null);

        await first.CreateAsync(New("Ocio"));
        await second.CreateAsync(New("Casa"));
        await anonymous.CreateAsync(New("Viajes"));

        Assert.Equal(3, await _database.Context.Categories.CountAsync());
    }

    // RF-10
    [Fact(DisplayName = "A household may create as many categories as it wants")]
    public async Task a_household_may_create_as_many_categories_as_it_wants()
    {
        for (var i = 0; i < 60; i++)
        {
            await _registrar.CreateAsync(New(string.Create(null, $"Categoría {i}")));
        }

        Assert.Equal(60, await _database.Context.Categories.CountAsync());
    }

    // RF-11
    [Fact(DisplayName = "A subcategory cannot become a parent")]
    public async Task a_subcategory_cannot_become_a_parent()
    {
        var parent = await Existing("Casa");
        var child = await Existing("Luz", parent: parent);

        var error = await Rejected(New("Contador", parentId: child.PublicId));

        Assert.Equal(
            "Solo se admite un nivel de subcategorías.",
            Assert.Single(error.Errors["parentId"]));
    }

    // RF-12
    [Fact(DisplayName = "A name longer than 80 characters is refused")]
    public async Task a_name_longer_than_80_characters_is_refused()
    {
        var error = await Rejected(New(new string('a', 81)));

        Assert.Equal(
            "El nombre no puede superar los 80 caracteres.",
            Assert.Single(error.Errors["name"]));
        Assert.Empty(_database.Context.Categories);
    }

    // RF-12, edge case: exactly 80 characters
    [Theory(DisplayName = "A name of exactly 80 characters is accepted")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task a_name_of_exactly_80_characters_is_accepted(bool padded)
    {
        var name = new string('a', 80);

        var created = await _registrar.CreateAsync(New(padded ? $"  {name}  " : name));

        Assert.Equal(name, created.Name);
    }

    // RF-13
    [Theory(DisplayName = "A parent of a different kind is refused")]
    [InlineData("expense", CategoryKind.Income)]
    [InlineData("income", CategoryKind.Expense)]
    public async Task a_parent_of_a_different_kind_is_refused(string kind, CategoryKind parentKind)
    {
        var parent = await Existing("Casa", parentKind);

        var error = await Rejected(New("Luz", kind: kind, parentId: parent.PublicId));

        Assert.Equal(
            "La subcategoría debe ser del mismo tipo que su categoría principal.",
            Assert.Single(error.Errors["parentId"]));
        Assert.Equal(1, await _database.Context.Categories.CountAsync());
    }

    private static CreateCategoryRequest New(
        string name, string kind = "expense", Guid? parentId = null) =>
        new(name, kind, parentId);

    private CategoryRegistrar NewRegistrar(
        LedgerDatabase? database = null, int? memberId = null) =>
        new(
            (database ?? _database).Context,
            new TestTenantContext(HouseholdId, memberId),
            new CreateCategoryRequestValidator());

    private async Task<ValidationFailedException> Rejected(CreateCategoryRequest request) =>
        await Assert.ThrowsAsync<ValidationFailedException>(
            () => _registrar.CreateAsync(request));

    private async Task<Category> Existing(
        string name,
        CategoryKind kind = CategoryKind.Expense,
        int householdId = HouseholdId,
        Category? parent = null,
        bool archived = false)
    {
        var category = Category.Create(householdId, name, kind, colorIndex: 1, parentId: parent?.Id);

        if (archived)
        {
            category.Archive();
        }

        _database.Context.Categories.Add(category);
        await _database.Context.SaveChangesAsync();

        return category;
    }
}
