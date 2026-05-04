using System.Reflection;
using YamlDotNet.RepresentationModel;

namespace ContactApp.Tests;

/// <summary>
/// Static-analysis tests for STORY-005: validates that docker-compose.yml
/// declares the `db` service with the correct PostgreSQL 16 image, port mapping,
/// environment variables, healthcheck, and named volume.
/// No Docker daemon is required — these tests parse the YAML file only.
/// </summary>
public class DockerComposeDbServiceTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Locates docker-compose.yml by walking upward from the test assembly's
    /// directory until the file is found or the root is reached.
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

    private static YamlMappingNode LoadRootMapping()
    {
        var yaml = new YamlStream();
        using var reader = new StreamReader(FindDockerComposeFile());
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    /// <summary>
    /// Returns the mapping node for the `db` service, or throws if absent.
    /// </summary>
    private static YamlMappingNode GetDbService()
    {
        var root = LoadRootMapping();
        var services = (YamlMappingNode)root["services"];
        return (YamlMappingNode)services["db"];
    }

    /// <summary>
    /// Reads every scalar value in a YAML sequence node as strings.
    /// </summary>
    private static IEnumerable<string> SequenceValues(YamlNode node)
    {
        var seq = (YamlSequenceNode)node;
        return seq.Children.OfType<YamlScalarNode>().Select(s => s.Value ?? string.Empty);
    }

    // ---------------------------------------------------------------------------
    // AC1 – postgres:16-alpine image and port 5432:5432
    // ---------------------------------------------------------------------------

    [Fact]
    public void AC1_DbService_UsesPostgres16AlpineImage()
    {
        // Arrange
        var db = GetDbService();

        // Act
        var image = ((YamlScalarNode)db["image"]).Value;

        // Assert
        Assert.Equal("postgres:16-alpine", image);
    }

    [Fact]
    public void AC1_DbService_ExposesPort5432OnHost()
    {
        // Arrange
        var db = GetDbService();

        // Act
        var ports = SequenceValues(db["ports"]).ToList();

        // Assert – the mapping "5432:5432" must be present
        Assert.Contains("5432:5432", ports);
    }

    [Fact]
    public void AC1_DbService_ImageIsNotWrongVersion()
    {
        // Negative: image must not be postgres:15 or unversioned `postgres:latest`
        var db = GetDbService();
        var image = ((YamlScalarNode)db["image"]).Value;

        Assert.DoesNotContain("postgres:15", image);
        Assert.DoesNotContain("postgres:latest", image);
    }

    [Fact]
    public void AC1_DbService_DoesNotExposeWrongPort()
    {
        // Negative: port mapping 5433:5432 (wrong host port) must not be present
        var db = GetDbService();
        var ports = SequenceValues(db["ports"]).ToList();

        Assert.DoesNotContain("5433:5432", ports);
    }

    // ---------------------------------------------------------------------------
    // AC2 – healthcheck runs pg_isready -U appuser -d contactdb
    // ---------------------------------------------------------------------------

    [Fact]
    public void AC2_DbService_HealthcheckExists()
    {
        // Arrange / Act
        var db = GetDbService();

        // Assert – the healthcheck key must be present
        Assert.True(db.Children.ContainsKey(new YamlScalarNode("healthcheck")),
            "The `db` service must declare a healthcheck.");
    }

    [Fact]
    public void AC2_DbService_HealthcheckTestContainsPgIsready()
    {
        // Arrange
        var db = GetDbService();
        var healthcheck = (YamlMappingNode)db["healthcheck"];
        var testValues = SequenceValues(healthcheck["test"]).ToList();

        // Act – join the test array into a single string for substring checks
        var testLine = string.Join(" ", testValues);

        // Assert – must reference pg_isready with the correct user and database
        Assert.Contains("pg_isready", testLine);
        Assert.Contains("-U appuser", testLine);
        Assert.Contains("-d contactdb", testLine);
    }

    [Fact]
    public void AC2_DbService_HealthcheckRetriesAllowAtLeast30SecondsStartup()
    {
        // Arrange
        var db = GetDbService();
        var healthcheck = (YamlMappingNode)db["healthcheck"];

        // Act – parse retries and interval so we can assert retries × interval >= 30 s
        var retriesNode = (YamlScalarNode)healthcheck["retries"];
        var retries = int.Parse(retriesNode.Value!);

        var intervalRaw = ((YamlScalarNode)healthcheck["interval"]).Value!;
        // interval is expressed as "<n>s" (e.g. "5s"); strip the trailing 's' to get seconds
        var intervalSeconds = int.Parse(intervalRaw.TrimEnd('s'));

        // Assert – retries x interval must be >= 30 seconds
        Assert.True(retries * intervalSeconds >= 30,
            $"Healthcheck window must be at least 30 seconds. Got retries={retries}, interval={intervalSeconds}s => {retries * intervalSeconds}s total.");
    }

    [Fact]
    public void AC2_DbService_HealthcheckTestDoesNotUseWrongUser()
    {
        // Negative: wrong user name must not appear in the test command
        var db = GetDbService();
        var healthcheck = (YamlMappingNode)db["healthcheck"];
        var testLine = string.Join(" ", SequenceValues(healthcheck["test"]));

        Assert.DoesNotContain("-U postgres", testLine);
    }

    [Fact]
    public void AC2_DbService_HealthcheckTestDoesNotUseWrongDatabase()
    {
        // Negative: wrong database name must not appear in the test command
        var db = GetDbService();
        var healthcheck = (YamlMappingNode)db["healthcheck"];
        var testLine = string.Join(" ", SequenceValues(healthcheck["test"]));

        Assert.DoesNotContain("-d postgres", testLine);
    }

    // ---------------------------------------------------------------------------
    // AC3 – named volume `pgdata` is declared and mounted (data persistence)
    // ---------------------------------------------------------------------------

    [Fact]
    public void AC3_TopLevel_DeclaresNamedVolumePgdata()
    {
        // Arrange
        var root = LoadRootMapping();

        // Act
        var volumes = (YamlMappingNode)root["volumes"];
        var volumeNames = volumes.Children.Keys
            .OfType<YamlScalarNode>()
            .Select(k => k.Value)
            .ToList();

        // Assert
        Assert.Contains("pgdata", volumeNames);
    }

    [Fact]
    public void AC3_DbService_MountsPgdataVolume()
    {
        // Arrange
        var db = GetDbService();
        var mounts = SequenceValues(db["volumes"]).ToList();

        // Assert – the volume mount must reference pgdata
        Assert.True(mounts.Any(m => m.StartsWith("pgdata:")),
            $"Expected a volume mount starting with 'pgdata:' but found: {string.Join(", ", mounts)}");
    }

    [Fact]
    public void AC3_DbService_VolumeMountTargetsPostgresDataDirectory()
    {
        // Arrange
        var db = GetDbService();
        var mounts = SequenceValues(db["volumes"]).ToList();

        // Act
        var pgdataMount = mounts.FirstOrDefault(m => m.StartsWith("pgdata:"));

        // Assert – must mount to the standard PostgreSQL data directory
        Assert.NotNull(pgdataMount);
        Assert.Equal("pgdata:/var/lib/postgresql/data", pgdataMount);
    }

    [Fact]
    public void AC3_DbService_DoesNotMountAnonymousVolume()
    {
        // Negative: an anonymous volume (no named reference) would not persist
        // across `docker compose down`. All mounts must use named volumes.
        var db = GetDbService();
        var mounts = SequenceValues(db["volumes"]).ToList();

        foreach (var mount in mounts)
        {
            // Must not be a host bind-mount (absolute path on the left side)
            Assert.False(mount.StartsWith("/"),
                $"Mount '{mount}' appears to be a bind mount, not a named volume.");
            // Must have a colon separator — anonymous volumes have no name portion
            Assert.Contains(":", mount);
        }
    }

    // ---------------------------------------------------------------------------
    // AC_EnvVars – environment variables required for the database to initialise
    // ---------------------------------------------------------------------------

    [Fact]
    public void AC_EnvVars_DbService_DeclaresPostgresDbEnvironmentVariable()
    {
        // Arrange
        var db = GetDbService();
        var env = (YamlMappingNode)db["environment"];
        var keys = env.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value).ToList();

        // Assert
        Assert.Contains("POSTGRES_DB", keys);
    }

    [Fact]
    public void AC_EnvVars_DbService_PostgresDbIsContactdb()
    {
        // Arrange
        var db = GetDbService();
        var env = (YamlMappingNode)db["environment"];

        // Act
        var value = ((YamlScalarNode)env["POSTGRES_DB"]).Value;

        // Assert
        Assert.Equal("contactdb", value);
    }

    [Fact]
    public void AC_EnvVars_DbService_DeclaresPostgresUserEnvironmentVariable()
    {
        var db = GetDbService();
        var env = (YamlMappingNode)db["environment"];
        var keys = env.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value).ToList();

        Assert.Contains("POSTGRES_USER", keys);
    }

    [Fact]
    public void AC_EnvVars_DbService_PostgresUserIsAppuser()
    {
        var db = GetDbService();
        var env = (YamlMappingNode)db["environment"];
        var value = ((YamlScalarNode)env["POSTGRES_USER"]).Value;

        Assert.Equal("appuser", value);
    }

    [Fact]
    public void AC_EnvVars_DbService_DeclaresPostgresPasswordEnvironmentVariable()
    {
        var db = GetDbService();
        var env = (YamlMappingNode)db["environment"];
        var keys = env.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value).ToList();

        Assert.Contains("POSTGRES_PASSWORD", keys);
    }

    [Fact]
    public void AC_EnvVars_DbService_PostgresPasswordIsNotEmpty()
    {
        // Negative: an empty password would prevent the container from starting
        var db = GetDbService();
        var env = (YamlMappingNode)db["environment"];
        var value = ((YamlScalarNode)env["POSTGRES_PASSWORD"]).Value;

        Assert.False(string.IsNullOrWhiteSpace(value),
            "POSTGRES_PASSWORD must not be empty.");
    }

    [Fact]
    public void AC_EnvVars_DbService_PostgresUserIsNotDefaultRootUser()
    {
        // Negative: using the default superuser name 'postgres' instead of
        // the application-specific 'appuser' would fail the pg_isready check
        // specified in AC2.
        var db = GetDbService();
        var env = (YamlMappingNode)db["environment"];
        var value = ((YamlScalarNode)env["POSTGRES_USER"]).Value;

        Assert.NotEqual("postgres", value);
    }

    // ---------------------------------------------------------------------------
    // AC4 – volume removal on `docker compose down -v` starts with empty database
    // ---------------------------------------------------------------------------

    [Fact]
    public void AC4_TopLevel_PgdataVolume_IsNotExternal()
    {
        // Arrange
        // If the pgdata volume were declared `external: true`, Docker Compose would
        // never remove it on `docker compose down -v`, meaning the next `up` would
        // NOT start with an empty database — violating AC4.
        var root = LoadRootMapping();
        var volumes = (YamlMappingNode)root["volumes"];

        // Act – locate the pgdata entry; it may be a null/empty scalar (no sub-keys)
        // or a mapping node. Either form is acceptable as long as `external: true`
        // is absent.
        var pgdataNode = volumes.Children
            .FirstOrDefault(kv => ((YamlScalarNode)kv.Key).Value == "pgdata")
            .Value;

        Assert.NotNull(pgdataNode); // the volume must be declared at all

        // A null scalar ("pgdata:") means no options — definitely not external
        if (pgdataNode is YamlMappingNode pgdataMapping)
        {
            var hasExternal = pgdataMapping.Children
                .Any(kv =>
                    ((YamlScalarNode)kv.Key).Value == "external" &&
                    ((YamlScalarNode)kv.Value).Value?.ToLowerInvariant() == "true");

            // Assert
            Assert.False(hasExternal,
                "The pgdata volume must NOT be declared with 'external: true'. " +
                "An external volume is not removed by 'docker compose down -v', " +
                "which would break the AC4 guarantee of starting with an empty database.");
        }
        // If pgdataNode is a YamlScalarNode (null value), external is not set — pass implicitly.
    }
}
