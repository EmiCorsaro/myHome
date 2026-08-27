using System.Reflection;
using NetArchTest.Rules;

namespace MyHome.Architecture.Tests;

public sealed class ModuleBoundaryTests
{
    private static readonly Assembly Shared = typeof(Modules.Shared.Domain.Money).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;
    private static readonly Assembly Ledger = Assembly.Load("MyHome.Modules.Ledger");

    private static readonly Assembly[] Modules = [Ledger];

    private const string ContractsSuffix = ".Contracts";

    private const string ApplicationSuffix = ".Application";

    private const string DomainSuffix = ".Domain";

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

    private static List<string> ImplementationNamespaces(Assembly module) =>
        TopLevelNamespaces(module, module.GetName().Name + ContractsSuffix);

    private static List<string> PrivateNamespaces(Assembly module) =>
        TopLevelNamespaces(
            module,
            module.GetName().Name + ContractsSuffix,
            module.GetName().Name + ApplicationSuffix);

    private static List<string> TopLevelNamespaces(Assembly module, params string[] excluded)
    {
        var root = module.GetName().Name + ".";

        return module.GetTypes()
            .Select(type => type.Namespace)
            .Where(name => name is not null && name.StartsWith(root, StringComparison.Ordinal))
            .Select(name => name!)
            .Where(name => !excluded.Any(
                prefix => name.StartsWith(prefix, StringComparison.Ordinal)))

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
