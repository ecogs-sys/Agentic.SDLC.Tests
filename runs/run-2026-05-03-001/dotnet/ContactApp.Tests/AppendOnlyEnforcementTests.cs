using System.Reflection;
using ContactApp.Api.Data;

namespace ContactApp.Tests;

/// <summary>
/// AC5 – Static tests that verify no Remove or Update code paths exist in the
/// production ContactApp.Api assembly.
///
/// Two complementary approaches are used:
///   1. Reflection over the ContactDbContext type – confirms the class exposes
///      no public/private methods whose names contain "Remove" or "Update".
///   2. Source-file scan of every *.cs file under ContactApp.Api – confirms that
///      none of the production source files call .Remove( or .Update( on any object.
///
/// Together these give high confidence that the append-only contract is honoured
/// both at the API surface and in the implementation text.
/// </summary>
public class AppendOnlyEnforcementTests
{
    // -------------------------------------------------------------------------
    // Reflection-based checks on ContactDbContext
    // -------------------------------------------------------------------------

    [Fact]
    public void ContactDbContext_HasNo_PublicMethod_ContainingRemove()
    {
        // Arrange
        var methods = typeof(ContactDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        Assert.Empty(methods);
    }

    [Fact]
    public void ContactDbContext_HasNo_PublicMethod_ContainingUpdate()
    {
        var methods = typeof(ContactDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.Contains("Update", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(methods);
    }

    [Fact]
    public void ContactDbContext_HasNo_PrivateMethod_ContainingRemove()
    {
        var methods = typeof(ContactDbContext)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(methods);
    }

    [Fact]
    public void ContactDbContext_HasNo_PrivateMethod_ContainingUpdate()
    {
        var methods = typeof(ContactDbContext)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.Contains("Update", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(methods);
    }

    // -------------------------------------------------------------------------
    // Source-file scan – ContactApp.Api production sources must not call
    // .Remove( or .Update( anywhere.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves the ContactApp.Api source root by walking up from the test
    /// assembly's location until we find the ContactApp.Api folder.
    /// </summary>
    private static string ResolveApiSourceRoot()
    {
        // Test assembly is at  …/ContactApp.Tests/bin/Debug/net10.0/
        // API source is at     …/ContactApp.Api/
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(typeof(AppendOnlyEnforcementTests).Assembly.Location)!);

        // Walk up until we reach the dotnet solution root (contains both
        // ContactApp.Api and ContactApp.Tests folders).
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "ContactApp.Api");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate ContactApp.Api source directory relative to the test assembly.");
    }

    [Fact]
    public void ApiSourceFiles_ContainNo_DotRemoveCall()
    {
        // Arrange
        var apiRoot = ResolveApiSourceRoot();
        var sourceFiles = Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        Assert.NotEmpty(sourceFiles); // sanity: we found at least one source file

        // Act – look for ".Remove(" in any production source file
        var violations = sourceFiles
            .SelectMany(file =>
                File.ReadAllLines(file)
                    .Select((line, idx) => (file, lineNumber: idx + 1, line))
                    .Where(t => t.line.Contains(".Remove(", StringComparison.Ordinal)))
            .ToList();

        // Assert
        Assert.True(
            violations.Count == 0,
            "Production source files must not call .Remove(). Violations found:\n" +
            string.Join("\n", violations.Select(v => $"  {v.file}:{v.lineNumber}  {v.line.Trim()}")));
    }

    [Fact]
    public void ApiSourceFiles_ContainNo_DotUpdateCall()
    {
        // Arrange
        var apiRoot = ResolveApiSourceRoot();
        var sourceFiles = Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        Assert.NotEmpty(sourceFiles);

        // Act – look for ".Update(" in any production source file
        var violations = sourceFiles
            .SelectMany(file =>
                File.ReadAllLines(file)
                    .Select((line, idx) => (file, lineNumber: idx + 1, line))
                    .Where(t => t.line.Contains(".Update(", StringComparison.Ordinal)))
            .ToList();

        // Assert
        Assert.True(
            violations.Count == 0,
            "Production source files must not call .Update(). Violations found:\n" +
            string.Join("\n", violations.Select(v => $"  {v.file}:{v.lineNumber}  {v.line.Trim()}")));
    }
}
