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
    private static readonly Assembly Shared = typeof(Modules.Shared.Money).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;
    private static readonly Assembly Ledger = Assembly.Load("MyHome.Modules.Ledger");

    /// <summary>Every module assembly in the solution, discovered by name.</summary>
    private static readonly Assembly[] Modules = [Ledger];

    /// <summary>Suffix marking the part of a module other modules are allowed to reach.</summary>
    private const string ContractsSuffix = ".Contracts";

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

    [Fact(DisplayName = "Modules reach each other only through contracts")]
    public void modules_reach_each_other_only_through_contracts()
    {
        // Dormant while there is one module. Written now because when the second one arrives it
        // would have to be negotiated against code that already breaks it.
        foreach (var module in Modules)
        {
            var forbidden = Modules
                .Where(other => other != module)
                .SelectMany(ImplementationNamespaces)
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
    /// The namespaces of a module that nobody outside it may depend on: everything except its
    /// contracts.
    /// </summary>
    /// <param name="module">Module assembly.</param>
    /// <returns>Top-level implementation namespaces, for instance <c>MyHome.Modules.Ledger.Domain</c>.</returns>
    private static IEnumerable<string> ImplementationNamespaces(Assembly module)
    {
        var root = module.GetName().Name + ".";
        var contracts = module.GetName().Name + ContractsSuffix;

        return module.GetTypes()
            .Select(type => type.Namespace)
            .Where(name => name is not null && name.StartsWith(root, StringComparison.Ordinal))
            .Select(name => name!)
            .Where(name => !name.StartsWith(contracts, StringComparison.Ordinal))

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
