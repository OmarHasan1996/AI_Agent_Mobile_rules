using System.Text;
using System.Text.Json;

namespace AI_Enoc_Engineering_Agent;

public sealed record RepositoryReview(
    string RepositoryPath,
    DateTime ReviewedAtUtc,
    IReadOnlyList<string> ScannedFiles,
    IReadOnlyList<ReviewFinding> Findings);

public sealed record ReviewFinding(
    string RuleId,
    string RuleName,
    string Severity,
    string FilePath,
    int LineNumber,
    string MatchedPattern);

public interface IRepositoryReviewService
{
    RepositoryReview Review(string repositoryPath, IReadOnlyList<EngineeringRule> rules);
    string Render(RepositoryReview review, string format);
}

public sealed class RepositoryReviewService : IRepositoryReviewService
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".dart", ".gradle", ".java", ".json", ".kt", ".kts", ".md", ".properties",
        ".swift", ".toml", ".ts", ".tsx", ".xml", ".yaml", ".yml"
    };

    public RepositoryReview Review(string repositoryPath, IReadOnlyList<EngineeringRule> rules)
    {
        if (!Directory.Exists(repositoryPath))
            throw new DirectoryNotFoundException($"Repository directory not found: {repositoryPath}");

        var root = Path.GetFullPath(repositoryPath);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(IsReviewableFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var findings = new List<ReviewFinding>();

        foreach (var file in files)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var rule in rules)
            {
                foreach (var pattern in rule.Forbidden.Where(pattern => !string.IsNullOrWhiteSpace(pattern)))
                {
                    for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                    {
                        if (lines[lineIndex].Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            findings.Add(new ReviewFinding(
                                rule.Id,
                                rule.Name,
                                rule.Severity,
                                Path.GetRelativePath(root, file),
                                lineIndex + 1,
                                pattern.Trim()));
                        }
                    }
                }
            }
        }

        return new RepositoryReview(root, DateTime.UtcNow, files.Select(path => Path.GetRelativePath(root, path)).ToArray(), findings);
    }

    public string Render(RepositoryReview review, string format) =>
        format.Equals("json", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.Serialize(review, new JsonSerializerOptions { WriteIndented = true })
            : ToMarkdown(review);

    private static bool IsReviewableFile(string path)
    {
        if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals(".git", StringComparison.OrdinalIgnoreCase)
                || part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || part.Equals("obj", StringComparison.OrdinalIgnoreCase)
                || part.Equals("node_modules", StringComparison.OrdinalIgnoreCase)))
            return false;

        return TextExtensions.Contains(Path.GetExtension(path));
    }

    private static string ToMarkdown(RepositoryReview review)
    {
        var output = new StringBuilder();
        output.AppendLine("# ENOC Repository Review");
        output.AppendLine();
        output.AppendLine($"- **Repository:** `{review.RepositoryPath}`");
        output.AppendLine($"- **Reviewed (UTC):** {review.ReviewedAtUtc:O}");
        output.AppendLine($"- **Files scanned:** {review.ScannedFiles.Count}");
        output.AppendLine($"- **Findings:** {review.Findings.Count}");
        output.AppendLine();
        output.AppendLine("## Findings");
        if (review.Findings.Count == 0)
        {
            output.AppendLine();
            output.AppendLine("No forbidden patterns from the selected guardrails were detected.");
            return output.ToString();
        }

        foreach (var finding in review.Findings)
        {
            output.AppendLine();
            output.AppendLine($"- **{finding.Severity} - {finding.RuleId}: {finding.RuleName}**");
            output.AppendLine($"  - `{finding.FilePath}:{finding.LineNumber}`");
            output.AppendLine($"  - Forbidden pattern: `{finding.MatchedPattern}`");
        }

        return output.ToString();
    }
}
