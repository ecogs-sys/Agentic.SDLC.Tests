using System.Reflection;
using YamlDotNet.RepresentationModel;

namespace ContactApp.Tests;

/// <summary>
/// Static-analysis tests for STORY-004: validates the backend Dockerfile and
/// the docker-compose.yml `api` service without requiring a Docker daemon.
///
/// Coverage:
///   AC1  — Dockerfile produces an image whose default command starts the API on http://+:8080
///   AC2  — api service healthcheck targets GET /api/health using curl
///   AC3  — structural compose checks (port 8080, depends_on db with service_healthy)
///   AC4  — api service declares all required environment variables
/// </summary>
public class DockerConfigTests
{
    // -------------------------------------------------------------------------
    // Path helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Walks up from the test assembly's output directory until docker-compose.yml
    /// is found. Returns the full path to the docker-compose.yml file.
    /// </summary>
    private static string FindDockerComposeFile()
    {
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "docker-compose.yml");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "docker-compose.yml was not found in any ancestor directory of the test assembly.");
    }

    /// <summary>
    /// Returns the full path to the backend Dockerfile, resolving it relative to
    /// the directory that contains docker-compose.yml.
    /// </summary>
    private static string FindDockerfile()
    {
        var composeFile = FindDockerComposeFile();
        var composeDir = Path.GetDirectoryName(composeFile)!;
        var dockerfile = Path.Combine(composeDir, "backend", "Dockerfile");

        if (!File.Exists(dockerfile))
            throw new FileNotFoundException(
                $"Dockerfile was not found at the expected path: {dockerfile}");

        return dockerfile;
    }

    private static string ReadDockerfile() => File.ReadAllText(FindDockerfile());

    private static YamlMappingNode LoadComposeRootMapping()
    {
        var yaml = new YamlStream();
        using var reader = new StreamReader(FindDockerComposeFile());
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    /// <summary>Returns the YAML mapping node for the `api` service.</summary>
    private static YamlMappingNode GetApiService()
    {
        var root = LoadComposeRootMapping();
        var services = (YamlMappingNode)root["services"];
        return (YamlMappingNode)services["api"];
    }

    // -------------------------------------------------------------------------
    // AC1 — Dockerfile inspection
    // -------------------------------------------------------------------------

    [Fact]
    public void AC1_Dockerfile_Exists()
    {
        // Arrange / Act — FindDockerfile() throws if absent; obtaining the path is the test
        var path = FindDockerfile();

        // Assert
        Assert.True(File.Exists(path),
            $"Dockerfile must exist at {path}.");
    }

    [Fact]
    public void AC1_Dockerfile_ContainsExposePort8080()
    {
        // Arrange
        var content = ReadDockerfile();

        // Act / Assert
        Assert.Contains("EXPOSE 8080", content);
    }

    [Fact]
    public void AC1_Dockerfile_DoesNotExposeWrongPort()
    {
        // Negative: port 80 (the old default) must not be exposed instead of 8080
        var content = ReadDockerfile();

        Assert.DoesNotContain("EXPOSE 80\n", content);
        Assert.DoesNotContain("EXPOSE 80\r", content);
    }

    [Fact]
    public void AC1_Dockerfile_SetsAspNetCoreUrlsToPort8080()
    {
        // Arrange
        var content = ReadDockerfile();

        // Assert — ENV instruction must bind to http://+:8080
        Assert.Contains("ASPNETCORE_URLS", content);
        Assert.Contains("http://+:8080", content);
    }

    [Fact]
    public void AC1_Dockerfile_DoesNotSetAspNetCoreUrlsToWrongPort()
    {
        // Negative: if ASPNETCORE_URLS pointed to a different port the API would
        // not be reachable on 8080 inside the container
        var content = ReadDockerfile();

        Assert.DoesNotContain("http://+:5000", content);
        Assert.DoesNotContain("http://+:80\n", content);
    }

    [Fact]
    public void AC1_Dockerfile_UsesTwoStageBuild_ContainsSdkFromLine()
    {
        // Arrange
        var content = ReadDockerfile();

        // Assert — first stage must use the SDK image
        Assert.Contains("mcr.microsoft.com/dotnet/sdk", content);
    }

    [Fact]
    public void AC1_Dockerfile_UsesTwoStageBuild_ContainsAspnetRuntimeFromLine()
    {
        // Arrange
        var content = ReadDockerfile();

        // Assert — second stage must use the aspnet runtime image (not the sdk)
        Assert.Contains("mcr.microsoft.com/dotnet/aspnet", content);
    }

    [Fact]
    public void AC1_Dockerfile_FinalStage_UsesAspnetRuntimeNotSdk()
    {
        // Arrange — locate all FROM lines
        var fromLines = ReadDockerfile()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("FROM ", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(fromLines.Count >= 2,
            $"Expected at least 2 FROM lines for a multi-stage build, found {fromLines.Count}.");

        // Act — the last FROM line is the final (runtime) stage
        var finalFrom = fromLines.Last();

        // Assert — final stage must be the aspnet runtime, not the full SDK
        Assert.Contains("aspnet", finalFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/sdk:", finalFrom, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC1_Dockerfile_EntrypointStartsContactAppApiDll()
    {
        // Arrange
        var content = ReadDockerfile();

        // Assert — ENTRYPOINT must reference the published DLL by name
        Assert.Contains("ContactApp.Api.dll", content);
    }

    [Fact]
    public void AC1_Dockerfile_EntrypointUsesDockerExecForm()
    {
        // Arrange
        var content = ReadDockerfile();

        // Assert — exec-form ENTRYPOINT looks like: ENTRYPOINT ["dotnet", ...]
        Assert.Contains("ENTRYPOINT [", content);
    }

    [Fact]
    public void AC1_Dockerfile_EntrypointDoesNotReferenceWrongDll()
    {
        // Negative: a typo in the DLL name would produce a container that exits immediately
        var content = ReadDockerfile();

        Assert.DoesNotContain("ContactApp.dll", content);  // missing ".Api" segment
    }

    // -------------------------------------------------------------------------
    // AC2 — healthcheck inspection (api service in compose)
    // -------------------------------------------------------------------------

    [Fact]
    public void AC2_ApiService_HealthcheckExists()
    {
        // Arrange / Act
        var api = GetApiService();

        // Assert
        Assert.True(api.Children.ContainsKey(new YamlScalarNode("healthcheck")),
            "The `api` service must declare a healthcheck.");
    }

    [Fact]
    public void AC2_ApiService_HealthcheckUsesCurl()
    {
        // Arrange
        var api = GetApiService();
        var healthcheck = (YamlMappingNode)api["healthcheck"];
        var testLine = GetHealthcheckTestLine(healthcheck);

        // Assert — curl is installed in the runtime image and used for the healthcheck
        Assert.Contains("curl", testLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC2_ApiService_HealthcheckDoesNotUseWget()
    {
        // Arrange
        var api = GetApiService();
        var healthcheck = (YamlMappingNode)api["healthcheck"];
        var testLine = GetHealthcheckTestLine(healthcheck);

        // Assert — wget is not used (curl is the installed tool)
        Assert.DoesNotContain("wget", testLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC2_ApiService_HealthcheckReferencesApiHealthPath()
    {
        // Arrange
        var api = GetApiService();
        var healthcheck = (YamlMappingNode)api["healthcheck"];
        var testLine = GetHealthcheckTestLine(healthcheck);

        // Assert — the health path must be the registered liveness endpoint
        Assert.Contains("/api/health", testLine);
    }

    [Fact]
    public void AC2_ApiService_HealthcheckDoesNotReferenceWrongPath()
    {
        // Negative: a path of just "/health" (without the /api prefix) would 404
        var api = GetApiService();
        var healthcheck = (YamlMappingNode)api["healthcheck"];
        var testLine = GetHealthcheckTestLine(healthcheck);

        // The path must include the /api prefix — naked /health must not be the only reference
        Assert.DoesNotContain("localhost:8080/health\"", testLine);
        Assert.DoesNotContain("localhost:8080/health'", testLine);
    }

    // -------------------------------------------------------------------------
    // AC3 structural checks — port and depends_on
    // -------------------------------------------------------------------------

    [Fact]
    public void AC3_ApiService_MapsPort8080()
    {
        // Arrange
        var api = GetApiService();
        var ports = GetSequenceValues(api["ports"]).ToList();

        // Assert
        Assert.Contains("8080:8080", ports);
    }

    [Fact]
    public void AC3_ApiService_DoesNotMapWrongPort()
    {
        // Negative: the spec mandates host:container 8080:8080; another mapping would be wrong
        var api = GetApiService();
        var ports = GetSequenceValues(api["ports"]).ToList();

        Assert.DoesNotContain("80:8080", ports);
        Assert.DoesNotContain("8080:80", ports);
    }

    [Fact]
    public void AC3_ApiService_DependsOnDbService()
    {
        // Arrange
        var api = GetApiService();

        // Assert — depends_on key must be present
        Assert.True(api.Children.ContainsKey(new YamlScalarNode("depends_on")),
            "The `api` service must declare depends_on.");

        // The value must reference `db`
        var dependsOn = api["depends_on"];
        var rawYaml = dependsOn.ToString();

        // Serialize the depends_on subtree to text for a simple string check
        Assert.True(
            DependsOnContainsService(dependsOn, "db"),
            "The `api` service depends_on must include `db`.");
    }

    [Fact]
    public void AC3_ApiService_DependsOnDb_WithConditionServiceHealthy()
    {
        // Arrange
        var api = GetApiService();
        var dependsOn = (YamlMappingNode)api["depends_on"];
        var dbEntry = (YamlMappingNode)dependsOn["db"];

        // Act
        var condition = ((YamlScalarNode)dbEntry["condition"]).Value;

        // Assert — must wait until the db healthcheck passes, not just start
        Assert.Equal("service_healthy", condition);
    }

    [Fact]
    public void AC3_ApiService_DependsOnDb_ConditionIsNotServiceStarted()
    {
        // Negative: service_started would allow the api to start before the
        // database accepts connections, causing EF Core migrations to fail
        var api = GetApiService();
        var dependsOn = (YamlMappingNode)api["depends_on"];
        var dbEntry = (YamlMappingNode)dependsOn["db"];
        var condition = ((YamlScalarNode)dbEntry["condition"]).Value;

        Assert.NotEqual("service_started", condition);
    }

    // -------------------------------------------------------------------------
    // AC4 — required environment variables in the api service
    // -------------------------------------------------------------------------

    [Fact]
    public void AC4_ApiService_Environment_ContainsAspNetCoreEnvironment()
    {
        // Arrange
        var keys = GetApiEnvKeys();

        // Assert
        Assert.Contains("ASPNETCORE_ENVIRONMENT", keys);
    }

    [Fact]
    public void AC4_ApiService_Environment_AspNetCoreEnvironmentIsNotEmpty()
    {
        // Negative: an empty value would leave ASP.NET Core without an environment name
        var env = GetApiEnvMapping();
        var value = ((YamlScalarNode)env["ASPNETCORE_ENVIRONMENT"]).Value;

        Assert.False(string.IsNullOrWhiteSpace(value),
            "ASPNETCORE_ENVIRONMENT must not be empty.");
    }

    [Fact]
    public void AC4_ApiService_Environment_ContainsAspNetCoreUrls()
    {
        // Arrange
        var keys = GetApiEnvKeys();

        // Assert
        Assert.Contains("ASPNETCORE_URLS", keys);
    }

    [Fact]
    public void AC4_ApiService_Environment_AspNetCoreUrlsBindsToPort8080()
    {
        // Arrange
        var env = GetApiEnvMapping();
        var value = ((YamlScalarNode)env["ASPNETCORE_URLS"]).Value;

        // Assert
        Assert.Contains("8080", value!);
    }

    [Fact]
    public void AC4_ApiService_Environment_AspNetCoreUrlsDoesNotBindToWrongPort()
    {
        // Negative: binding to port 5000 would make the healthcheck probe fail
        var env = GetApiEnvMapping();
        var value = ((YamlScalarNode)env["ASPNETCORE_URLS"]).Value;

        Assert.DoesNotContain("5000", value!);
    }

    [Fact]
    public void AC4_ApiService_Environment_ContainsConnectionStringsDefaultConnection()
    {
        // Arrange
        var keys = GetApiEnvKeys();

        // Assert
        Assert.Contains("ConnectionStrings__DefaultConnection", keys);
    }

    [Fact]
    public void AC4_ApiService_Environment_ConnectionStringReferencesDbHost()
    {
        // Arrange — within the compose network the db service is reachable as "db"
        var env = GetApiEnvMapping();
        var value = ((YamlScalarNode)env["ConnectionStrings__DefaultConnection"]).Value;

        // Assert
        Assert.Contains("Host=db", value!);
    }

    [Fact]
    public void AC4_ApiService_Environment_ConnectionStringDoesNotUseLocalhostForDb()
    {
        // Negative: "localhost" would not resolve to the Postgres container inside compose
        var env = GetApiEnvMapping();
        var value = ((YamlScalarNode)env["ConnectionStrings__DefaultConnection"]).Value;

        Assert.DoesNotContain("Host=localhost", value!);
    }

    [Fact]
    public void AC4_ApiService_Environment_ContainsCorsAllowedOrigin()
    {
        // Arrange
        var keys = GetApiEnvKeys();

        // Assert
        Assert.Contains("Cors__AllowedOrigin", keys);
    }

    [Fact]
    public void AC4_ApiService_Environment_CorsAllowedOriginIsNotEmpty()
    {
        // Negative: an empty CORS origin would cause all preflight requests to be rejected
        var env = GetApiEnvMapping();
        var value = ((YamlScalarNode)env["Cors__AllowedOrigin"]).Value;

        Assert.False(string.IsNullOrWhiteSpace(value),
            "Cors__AllowedOrigin must not be empty.");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static IEnumerable<string> GetSequenceValues(YamlNode node)
    {
        var seq = (YamlSequenceNode)node;
        return seq.Children.OfType<YamlScalarNode>().Select(s => s.Value ?? string.Empty);
    }

    private static string GetHealthcheckTestLine(YamlMappingNode healthcheck)
    {
        var testValues = GetSequenceValues(healthcheck["test"]).ToList();
        return string.Join(" ", testValues);
    }

    private static YamlMappingNode GetApiEnvMapping()
    {
        var api = GetApiService();
        return (YamlMappingNode)api["environment"];
    }

    private static List<string?> GetApiEnvKeys()
    {
        var env = GetApiEnvMapping();
        return env.Children.Keys
            .OfType<YamlScalarNode>()
            .Select(k => k.Value)
            .ToList();
    }

    /// <summary>
    /// Returns true if the depends_on node (which may be a sequence or mapping)
    /// contains an entry keyed by <paramref name="serviceName"/>.
    /// </summary>
    private static bool DependsOnContainsService(YamlNode dependsOn, string serviceName)
    {
        if (dependsOn is YamlMappingNode map)
            return map.Children.Keys.OfType<YamlScalarNode>().Any(k => k.Value == serviceName);

        if (dependsOn is YamlSequenceNode seq)
            return seq.Children.OfType<YamlScalarNode>().Any(s => s.Value == serviceName);

        return false;
    }
}
