using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AI_Enoc_Engineering_Agent;

public sealed class RuleCatalogDocument
{
    [YamlMember(Alias = "rules")]
    public List<EngineeringRule> Rules { get; init; } = new();
}

public sealed class EngineeringRule
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> Requirements { get; init; } = new();
    public List<string> Forbidden { get; init; } = new();
    public List<string> Enforcement { get; init; } = new();
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

public static class RuleCatalogLoader
{
    public static IReadOnlyList<EngineeringRule> Load(string rulesDirectory)
    {
        if (!Directory.Exists(rulesDirectory))
        {
            throw new DirectoryNotFoundException($"Rules directory not found: {rulesDirectory}");
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var rules = new List<EngineeringRule>();
        foreach (var file in Directory.EnumerateFiles(rulesDirectory, "*.yaml").OrderBy(path => path))
        {
            if (Path.GetFileName(file).Equals("config.yaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var document = deserializer.Deserialize<RuleCatalogDocument>(File.ReadAllText(file));
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

        return rules
            .OrderBy(rule => CategoryRank(rule.Category))
            .ThenByDescending(rule => SeverityRank(rule.Severity))
            .ThenBy(rule => rule.Id)
            .ToArray();
    }

    private static int CategoryRank(string category) => category.ToLowerInvariant() switch
    {
        "architecture" => 1,
        "security" => 2,
        "testing" => 3,
        "observability" => 4,
        "environments" => 5,
        "cicd" => 6,
        _ => 99
    };

    private static int SeverityRank(string severity) => severity.ToUpperInvariant() switch
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
