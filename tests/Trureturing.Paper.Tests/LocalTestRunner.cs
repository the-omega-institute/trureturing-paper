using System.Reflection;
using Xunit;

namespace Trureturing.Paper.Tests;

internal static class LocalTestRunner
{
    public static int Main()
    {
        var failures = 0;
        var passed = 0;
        var skipped = 0;
        var tests = typeof(LocalTestRunner).Assembly.GetTypes()
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .SelectMany(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(method => (Type: type, Method: method, Fact: method.GetCustomAttribute<FactAttribute>())))
            .Where(static test => test.Fact is not null)
            .OrderBy(static test => test.Method.Name, StringComparer.Ordinal);

        foreach (var test in tests)
        {
            var name = $"{test.Type.FullName}.{test.Method.Name}";
            if (test.Fact!.Skip is not null)
            {
                Console.WriteLine($"[SKIP] {name}: {test.Fact.Skip}");
                skipped++;
                continue;
            }

            try
            {
                var instance = Activator.CreateInstance(test.Type);
                test.Method.Invoke(instance, null);
                Console.WriteLine($"[PASS] {name}");
                passed++;
            }
            catch (TargetInvocationException exception)
            {
                Console.WriteLine($"[FAIL] {name}: {exception.InnerException}");
                failures++;
            }
        }

        Console.WriteLine($"Total: {passed + failures + skipped}; Passed: {passed}; Failed: {failures}; Skipped: {skipped}");
        return failures == 0 ? 0 : 1;
    }
}
