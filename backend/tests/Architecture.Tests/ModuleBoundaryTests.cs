using System.Reflection;
using NetArchTest.Rules;

namespace MyHome.Architecture.Tests;

/// <summary>
/// The modular monolith's boundaries, checked on every build.
/// </summary>
/// <remarks>
/// Written from the first commit, while nothing violates them yet. Adding them once twenty screens
/// exist turns each one into archaeology and negotiation.
/// <para>
/// What they protect is not purity: it is that extracting a module to its own service stays a
/// deployment change instead of a redesign.
/// </para>
/// </remarks>
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly Shared = typeof(Modules.Shared.Domain.Money).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;
    private static readonly Assembly Ledger = Assembly.Load("MyHome.Modules.Ledger");

    /// <summary>Every module assembly in the solution, discovered by name.</summary>
    private static readonly Assembly[] Modules = [Ledger];

    /// <summary>Namespace holding a module's data contracts: the shapes it exchanges.</summary>
    private const string ContractsSuffix = ".Contracts";

    /// <summary>
    /// Namespace holding a module's service interfaces and the services fulfilling them.
    /// </summary>
    /// <remarks>
    /// Published together with the contracts, which is unusual and deliberate: the interfaces sit
    /// beside their implementations rather than in a separate contracts namespace. What keeps the
    /// implementation private is not the namespace but visibility — see
    /// <see cref="a_modules_services_are_not_reachable_from_outside"/>, which is the test that
    /// makes publishing this namespace safe.
    /// </remarks>
    private const string ApplicationSuffix = ".Application";

    /// <summary>Namespace holding a module's entities and rules. Depends on nothing.</summary>
    private const string DomainSuffix = ".Domain";

    /// <summary>Namespace holding a module's mapping, context and migrations.</summary>
    private const string PersistenceSuffix = ".Persistence";

    [Fact(DisplayName = "The shared kernel knows about no module")]
    public void shared_kernel_does_not_depend_on_modules()
    {
        var result = Types.InAssembly(Shared)
            .Should()
            .NotHaveDependencyOnAny("MyHome.Modules.Ledger", "MyHome.Api")
            .GetResult();

        AssertSuccess(
            result,
            "The shared kernel defines the common vocabulary. If it knew about a module, that " +
            "module could no longer be extracted.");
    }

    [Fact(DisplayName = "A module's contracts do not depend on its implementation")]
    public void contracts_do_not_depend_on_implementation()
    {
        // Replaces the separate Contracts project. That made the rule impossible to break; this
        // makes it impossible to break unnoticed, and costs one project less to explain.
        //
        // It matters inside a single module too: the contracts are what clients see, and a DTO
        // that leaks a domain entity turns every internal rename into a breaking API change.
        foreach (var module in Modules)
        {
            var contractsNamespace = module.GetName().Name + ContractsSuffix;

            var result = Types.InAssembly(module)
                .That()
                .ResideInNamespaceStartingWith(contractsNamespace)
                .Should()
                .NotHaveDependencyOnAny([.. ImplementationNamespaces(module)])
                .GetResult();

            AssertSuccess(
                result,
                $"The contracts of {module.GetName().Name} must not reach into its own " +
                "implementation: they are the published shape, not a view of the domain.");
        }
    }

    [Fact(DisplayName = "A module's services are not reachable from outside")]
    public void a_modules_services_are_not_reachable_from_outside()
    {
        // This is what makes it safe to publish the Application namespace. The service interfaces
        // live beside their implementations, so the namespace alone no longer separates the
        // published surface from the internals: visibility does. Every service is internal, so an
        // outside assembly resolving this namespace finds only the interfaces.
        //
        // Without this test the arrangement is one `public` keyword away from letting a caller
        // construct a service directly, tenant filter and all.
        foreach (var module in Modules)
        {
            var applicationNamespace = module.GetName().Name + ApplicationSuffix;

            var result = Types.InAssembly(module)
                .That()
                .ResideInNamespaceStartingWith(applicationNamespace)
                .And()
                .ArePublic()
                .Should()
                .BeInterfaces()
                .GetResult();

            AssertSuccess(
                result,
                $"Only interfaces may be public in {module.GetName().Name}'s application layer. " +
                "A public service turns an implementation detail into part of the module's API, " +
                "and callers will bypass the interface the moment it is convenient.");
        }
    }

    [Fact(DisplayName = "A module's domain depends on nothing but the shared kernel")]
    public void a_modules_domain_depends_on_nothing()
    {
        // The other tests in this file guard the horizontal axis: module against module, HTTP
        // against database. This one guards the vertical, which is the dependency rule itself —
        // the domain sits at the centre and points at nobody.
        //
        // The module's project file brings EF Core in for the whole assembly, so nothing stops an
        // entity from growing a mapping attribute or a lazy-loading navigation. Written while the
        // domain is still clean, so it stays that way rather than being argued about later.
        foreach (var module in Modules)
        {
            var root = module.GetName().Name;

            var result = Types.InAssembly(module)
                .That()
                .ResideInNamespaceStartingWith(root + DomainSuffix)
                .Should()
                .NotHaveDependencyOnAny(
                    "Microsoft.EntityFrameworkCore",
                    "Npgsql",
                    "FluentValidation",
                    root + ApplicationSuffix,
                    root + PersistenceSuffix)
                .GetResult();

            AssertSuccess(
                result,
                $"The domain of {root} must be runnable without a database and without a " +
                "container: it is where the rules live, and a rule that needs EF Core to run " +
                "can only be tested by starting one.");
        }
    }

    [Fact(DisplayName = "A module's published interfaces do not expose its domain")]
    public void published_interfaces_do_not_expose_the_domain()
    {
        // What the contracts test used to give the interfaces for free while they lived in the
        // Contracts namespace. They no longer do, so the rule is stated on its own: a signature
        // returning a domain entity turns every internal rename into a breaking API change.
        foreach (var module in Modules)
        {
            var applicationNamespace = module.GetName().Name + ApplicationSuffix;

            var result = Types.InAssembly(module)
                .That()
                .ResideInNamespaceStartingWith(applicationNamespace)
                .And()
                .AreInterfaces()
                .Should()
                .NotHaveDependencyOnAny([.. PrivateNamespaces(module)])
                .GetResult();

            AssertSuccess(
                result,
                $"The published interfaces of {module.GetName().Name} must speak in contracts, " +
                "not in entities or persistence types.");
        }
    }

    [Fact(DisplayName = "Modules reach each other only through the published surface")]
    public void modules_reach_each_other_only_through_the_published_surface()
    {
        // Dormant while there is one module. Written now because when the second one arrives it
        // would have to be negotiated against code that already breaks it.
        foreach (var module in Modules)
        {
            var forbidden = Modules
                .Where(other => other != module)
                .SelectMany(PrivateNamespaces)
                .ToArray();

            if (forbidden.Length == 0)
            {
                continue;
            }

            var result = Types.InAssembly(module)
                .Should()
                .NotHaveDependencyOnAny(forbidden)
                .GetResult();

            AssertSuccess(
                result,
                $"{module.GetName().Name} reaches into another module's internals. Modules talk " +
                "through contracts and events; anything else makes them impossible to separate.");
        }
    }

    /// <summary>
    /// Everything in a module that is not its data contracts.
    /// </summary>
    /// <param name="module">Module assembly.</param>
    /// <returns>Top-level namespaces, for instance <c>MyHome.Modules.Ledger.Domain</c>.</returns>
    private static List<string> ImplementationNamespaces(Assembly module) =>
        TopLevelNamespaces(module, module.GetName().Name + ContractsSuffix);

    /// <summary>
    /// The namespaces of a module that nobody outside it may depend on: everything except the two
    /// it publishes, its contracts and its application layer.
    /// </summary>
    /// <param name="module">Module assembly.</param>
    /// <returns>Top-level private namespaces, for instance <c>MyHome.Modules.Ledger.Domain</c>.</returns>
    private static List<string> PrivateNamespaces(Assembly module) =>
        TopLevelNamespaces(
            module,
            module.GetName().Name + ContractsSuffix,
            module.GetName().Name + ApplicationSuffix);

    /// <summary>
    /// Lists a module's top-level namespaces, dropping the ones given.
    /// </summary>
    /// <param name="module">Module assembly.</param>
    /// <param name="excluded">Namespace prefixes to leave out.</param>
    /// <returns>One entry per remaining first segment after the module root.</returns>
    private static List<string> TopLevelNamespaces(Assembly module, params string[] excluded)
    {
        var root = module.GetName().Name + ".";

        return module.GetTypes()
            .Select(type => type.Namespace)
            .Where(name => name is not null && name.StartsWith(root, StringComparison.Ordinal))
            .Select(name => name!)
            .Where(name => !excluded.Any(
                prefix => name.StartsWith(prefix, StringComparison.Ordinal)))

            // First segment after the module root only (Domain, Application, Persistence):
            // NetArchTest matches by prefix, so nested namespaces are already covered.
            .Select(name => root + name[root.Length..].Split('.')[0])
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    [Fact(DisplayName = "The HTTP layer cannot reach the database")]
    public void api_does_not_depend_on_entity_framework()
    {
        var result = Types.InAssembly(Api)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        AssertSuccess(
            result,
            "Endpoints are thin: they bind, delegate to a service, map and respond. If they " +
            "can query the database, logic will end up in there and there will be no way to " +
            "invoke it without going through HTTP.");
    }

    [Fact(DisplayName = "A module's domain does not depend on the HTTP layer")]
    public void ledger_does_not_depend_on_aspnet()
    {
        var result = Types.InAssembly(Ledger)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.AspNetCore", "MyHome.Api")
            .GetResult();

        AssertSuccess(
            result,
            "Every business rule must be runnable from a test, a background job or a second " +
            "client, without starting a web server.");
    }

    private static void AssertSuccess(TestResult result, string why)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        var offenders = string.Join(
            Environment.NewLine,
            (result.FailingTypeNames ?? []).Select(name => $"  - {name}"));

        Assert.Fail(
            $"{why}{Environment.NewLine}{Environment.NewLine}" +
            $"Offending types:{Environment.NewLine}{offenders}");
    }
}
