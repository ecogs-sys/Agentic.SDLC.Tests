using System.Runtime.CompilerServices;
using System.IO;

internal static class TestEnvironment
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Testcontainers URI parser strips the '.' from the Windows named pipe path,
        // producing an invalid URI. Set DOCKER_HOST explicitly in the correct format
        // before Testcontainers' static initializer runs.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            Environment.SetEnvironmentVariable(
                "DOCKER_HOST",
                "npipe://./pipe/dockerDesktopLinuxEngine");
        }

        // Also fix ~/.testcontainers.properties if it contains the 4-slash format
        // that Docker Desktop reports. Testcontainers reads this file during its static
        // initializer and the 4-slash format ("npipe:////./pipe/...") causes a URI parse
        // failure. Replace with the 2-slash format ("npipe://./pipe/...") which is valid.
        try
        {
            var propsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".testcontainers.properties");

            if (File.Exists(propsPath))
            {
                var content = File.ReadAllText(propsPath);
                if (content.Contains("npipe:////./pipe/"))
                {
                    File.WriteAllText(propsPath,
                        content.Replace("npipe:////./pipe/", "npipe://./pipe/"));
                }
            }
        }
        catch
        {
            // Best-effort: if we cannot read/write the properties file, continue.
            // The DOCKER_HOST env var set above is the primary fix.
        }
    }
}
