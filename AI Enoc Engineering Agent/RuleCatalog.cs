using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AI_Enoc_Engineering_Agent;

public sealed class RuleCatalogDocument
{
    [YamlMember(Alias = "rules")]
    public List<EngineeringRule> Rules { get; init; } = new();
}

public sealed class RuleCatalogConfig
{
    [YamlMember(Alias = "schema_version")]
    public string SchemaVersion { get; init; } = string.Empty;
    public Dictionary<string, object> Severity { get; init; } = new();
    [YamlMember(Alias = "enforcement_types")]
    public Dictionary<string, object> EnforcementTypes { get; init; } = new();
    public LifecycleConfig Lifecycle { get; init; } = new();
    public RuleDefaults Defaults { get; init; } = new();
}

public sealed class LifecycleConfig
{
    public List<string> States { get; init; } = new();
}

public sealed class RuleDefaults
{
    public string Version { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public RuleScope Scope { get; init; } = new();
    public DefaultEvidence Evidence { get; init; } = new();
}

public sealed class DefaultEvidence
{
    public string Source { get; init; } = string.Empty;
}

public sealed class RuleScope
{
    public List<string> Platforms { get; init; } = new();
    [YamlMember(Alias = "project_types")]
    public List<string> ProjectTypes { get; init; } = new();
    public List<string> Environments { get; init; } = new();
    public List<string> Languages { get; init; } = new();
}

public sealed class EngineeringRule
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public RuleScope? Scope { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<string> Requirements { get; init; } = new();
    public List<string> Forbidden { get; init; } = new();
    public List<string> Enforcement { get; init; } = new();
    public RuleValidation? Validation { get; init; }
    public AiGuidance? AiGuidance { get; init; }
    public Evidence? Evidence { get; init; }
}

public sealed class AiGuidance
{
    public List<string> Instructions { get; init; } = new();
    public List<string> ReviewQuestions { get; init; } = new();
}

public sealed class Evidence
{
    public string Reference { get; init; } = string.Empty;
}

public sealed class RuleValidation
{
    public string Type { get; init; } = string.Empty;
    public List<string> Checks { get; init; } = new();
}

public static class RuleCatalogLoader
{
    private static readonly HashSet<string> ValidSeverities =
        new(StringComparer.OrdinalIgnoreCase) { "BLOCKER", "CRITICAL", "HIGH", "MEDIUM", "LOW", "INFO" };
    private static readonly HashSet<string> ValidCategories =
        new(StringComparer.OrdinalIgnoreCase) { "architecture", "security", "testing", "observability", "environments", "cicd" };

    public static RuleCatalog Load(string rulesDirectory)
    {
        if (!Directory.Exists(rulesDirectory))
        {
            throw new DirectoryNotFoundException($"Rules directory not found: {rulesDirectory}");
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var configPath = Path.Combine(rulesDirectory, "config.yaml");
        if (!File.Exists(configPath))
        {
            throw new InvalidDataException($"Required catalog configuration not found: {configPath}");
        }

        RuleCatalogConfig? config;
        try
        {
            config = deserializer.Deserialize<RuleCatalogConfig>(File.ReadAllText(configPath));
        }
        catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or InvalidOperationException)
        {
            throw new InvalidDataException($"Invalid catalog configuration: {exception.Message}", exception);
        }

        if (config is null)
        {
            throw new InvalidDataException("Catalog configuration is empty.");
        }
        ValidateConfig(config);

        var rules = new List<EngineeringRule>();
        foreach (var file in Directory.EnumerateFiles(rulesDirectory, "*.yaml").OrderBy(path => path))
        {
            if (Path.GetFileName(file).Equals("config.yaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            RuleCatalogDocument? document;
            try
            {
                document = deserializer.Deserialize<RuleCatalogDocument>(File.ReadAllText(file));
            }
            catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or InvalidOperationException)
            {
                throw new InvalidDataException($"Invalid rule file '{Path.GetFileName(file)}': {exception.Message}", exception);
            }

            if (document?.Rules is null)
            {
                continue;
            }

            rules.AddRange(document.Rules);
        }

        var duplicate = rules
            .GroupBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Duplicate rule id found: {duplicate.Key}");
        }

        foreach (var rule in rules)
        {
            ValidateRule(rule, config.Defaults);
        }

        var orderedRules = rules
            .OrderBy(rule => CategoryRank(rule.Category))
            .ThenByDescending(rule => SeverityRank(rule.Severity))
            .ThenBy(rule => rule.Id)
            .ToArray();
        return RuleCatalog.Create(config.SchemaVersion, config.Defaults, orderedRules);
    }

    private static void ValidateConfig(RuleCatalogConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.SchemaVersion))
            throw new InvalidDataException("Catalog schema_version is required.");
        if (string.IsNullOrWhiteSpace(config.Defaults.Version) || string.IsNullOrWhiteSpace(config.Defaults.Status))
            throw new InvalidDataException("Catalog defaults.version and defaults.status are required.");
        if (config.Defaults.Scope.Platforms.Count == 0 || config.Defaults.Scope.ProjectTypes.Count == 0 || config.Defaults.Scope.Environments.Count == 0)
            throw new InvalidDataException("Catalog defaults.scope must define platforms, project_types, and environments.");
    }

    private static void ValidateRule(EngineeringRule rule, RuleDefaults defaults)
    {
        if (string.IsNullOrWhiteSpace(rule.Id) || string.IsNullOrWhiteSpace(rule.Name) ||
            string.IsNullOrWhiteSpace(rule.Category) || string.IsNullOrWhiteSpace(rule.Severity))
            throw new InvalidDataException($"Rule '{rule.Id}' is missing a required field.");
        if (!ValidCategories.Contains(rule.Category))
            throw new InvalidDataException($"Rule '{rule.Id}' has unsupported category '{rule.Category}'.");
        if (!ValidSeverities.Contains(rule.Severity))
            throw new InvalidDataException($"Rule '{rule.Id}' has unsupported severity '{rule.Severity}'.");
    }

    internal static int CategoryRank(string category) => category.ToLowerInvariant() switch
    {
        "architecture" => 1,
        "security" => 2,
        "testing" => 3,
        "observability" => 4,
        "environments" => 5,
        "cicd" => 6,
        _ => 99
    };

    internal static int SeverityRank(string severity) => severity.ToUpperInvariant() switch
    {
        "BLOCKER" => 6,
        "CRITICAL" => 5,
        "HIGH" => 4,
        "MEDIUM" => 3,
        "LOW" => 2,
        "INFO" => 1,
        _ => 0
    };
}

public sealed record RuleCatalog(
    string SchemaVersion,
    RuleDefaults Defaults,
    IReadOnlyList<EngineeringRule> Rules)
{
    public static RuleCatalog Create(string schemaVersion, RuleDefaults defaults, IEnumerable<EngineeringRule> rules) =>
        new(schemaVersion, defaults, rules
            .OrderBy(rule => RuleCatalogLoader.CategoryRank(rule.Category))
            .ThenByDescending(rule => RuleCatalogLoader.SeverityRank(rule.Severity))
            .ThenBy(rule => rule.Id)
            .ToArray());

    public IReadOnlyList<EngineeringRule> ApplicableTo(ContractContext context) =>
        Rules.Where(rule =>
        {
            var scope = rule.Scope ?? Defaults.Scope;
            return IsMatch(scope.Platforms, context.Platform)
                && IsMatch(scope.ProjectTypes, context.ProjectType)
                && IsMatch(scope.Environments, context.Environment)
                && (scope.Languages.Count == 0 || IsMatch(scope.Languages, context.DevelopmentLanguage))
                && string.Equals(rule.Status.Length == 0 ? Defaults.Status : rule.Status, "active", StringComparison.OrdinalIgnoreCase);
        }).ToArray();

    private static bool IsMatch(IReadOnlyCollection<string> values, string value) =>
        values.Count == 0 || values.Contains(value, StringComparer.OrdinalIgnoreCase);

}
