using System.Text;
using System.Text.Json;

namespace AI_Enoc_Engineering_Agent;

public sealed record ContractContext(
    string Task,
    string Platform,
    string DevelopmentLanguage,
    string Environment,
    string ProjectType);

public sealed class EngineeringContract
{
    public string CatalogSchemaVersion { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    public ContractContext Context { get; init; } = new("", "", "", "", "");
    public IReadOnlyList<EngineeringRule> Rules { get; init; } = Array.Empty<EngineeringRule>();
}

public static class EngineeringContractGenerator
{
    public static EngineeringContract Create(ContractContext context, IReadOnlyList<EngineeringRule> rules, string catalogSchemaVersion = "unknown") =>
        new() { CatalogSchemaVersion = catalogSchemaVersion, Context = context, Rules = rules };

    public static string ToMarkdown(EngineeringContract contract)
    {
        var output = new StringBuilder();
        output.AppendLine("# ENOC Engineering Guardrails");
        output.AppendLine();
        output.AppendLine("Use this contract as the non-negotiable engineering policy for the requested work.");
        output.AppendLine();
        output.AppendLine("## Context");
        output.AppendLine($"- **Catalog schema:** {EscapeInline(contract.CatalogSchemaVersion)}");
        output.AppendLine($"- **Generated (UTC):** {contract.GeneratedAtUtc:O}");
        output.AppendLine($"- **Task:** {EscapeInline(contract.Context.Task)}");
        output.AppendLine($"- **Platform:** {EscapeInline(contract.Context.Platform)}");
        output.AppendLine($"- **Development language:** {EscapeInline(contract.Context.DevelopmentLanguage)}");
        output.AppendLine($"- **Environment:** {EscapeInline(contract.Context.Environment)}");
        output.AppendLine($"- **Project type:** {EscapeInline(contract.Context.ProjectType)}");
        output.AppendLine();
        output.AppendLine("## Agent instructions");
        output.AppendLine("- Act as a Principal Mobile Engineer.");
        output.AppendLine("- Follow every applicable requirement below; do not silently weaken a BLOCKER or CRITICAL rule.");
        output.AppendLine("- Explain any rule conflict or missing implementation evidence before completing the task.");
        output.AppendLine();
        output.AppendLine("## Applicable rules");

        foreach (var rule in contract.Rules)
        {
            output.AppendLine();
            output.AppendLine($"### {rule.Id}: {rule.Name} ({rule.Severity})");
            output.AppendLine(rule.Description.Trim());
            AppendList(output, "Requirements", rule.Requirements);
            AppendList(output, "Forbidden", rule.Forbidden);
            AppendList(output, "AI instructions", rule.AiGuidance?.Instructions);
            AppendList(output, "Review questions", rule.AiGuidance?.ReviewQuestions);
            AppendList(output, "Enforcement", rule.Enforcement);
        }

        output.AppendLine();
        output.AppendLine("## Completion gate");
        output.AppendLine("- Report changed files and tests run.");
        output.AppendLine("- Confirm each BLOCKER and CRITICAL rule has evidence.");
        output.AppendLine("- Do not claim compliance for a gate that was not executed.");
        return output.ToString();
    }

    public static string ToJson(EngineeringContract contract) =>
        JsonSerializer.Serialize(contract, new JsonSerializerOptions { WriteIndented = true });

    private static void AppendList(StringBuilder output, string title, IEnumerable<string>? values)
    {
        var items = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? Array.Empty<string>();
        if (items.Length == 0)
        {
            return;
        }

        output.AppendLine($"**{title}:**");
        foreach (var item in items)
        {
            output.AppendLine($"- {EscapeInline(item.Trim())}");
        }
    }

    private static string EscapeInline(string value) =>
        value.Replace("\\", "\\\\")
            .Replace("`", "\\`")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("#", "\\#")
            .Replace("\r", " ")
            .Replace("\n", " ");
}
