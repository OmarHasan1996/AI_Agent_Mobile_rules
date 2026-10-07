using AI_Enoc_Engineering_Agent;
using Xunit;

namespace AI_Enoc_Engineering_Agent.Tests;

public sealed class RuleCatalogTests
{
    [Fact]
    public void ApplicableTo_UsesContextAndActiveStatus()
    {
        var defaults = new RuleDefaults
        {
            Status = "active",
            Scope = new RuleScope
            {
                Platforms = new List<string> { "android" },
                ProjectTypes = new List<string> { "mobile" },
                Environments = new List<string> { "production" },
                Languages = new List<string> { "Kotlin" }
            }
        };
        var catalog = RuleCatalog.Create("1.0", defaults, new[]
        {
            new EngineeringRule { Id = "SEC-001", Name = "Secure", Category = "security", Severity = "CRITICAL" },
            new EngineeringRule { Id = "DRAFT-001", Name = "Draft", Category = "security", Severity = "HIGH", Status = "draft" }
        });

        var matching = catalog.ApplicableTo(new ContractContext("Login", "android", "Kotlin", "production", "mobile"));
        var nonMatching = catalog.ApplicableTo(new ContractContext("Login", "ios", "Swift", "production", "mobile"));

        Assert.Single(matching);
        Assert.Equal("SEC-001", matching[0].Id);
        Assert.Empty(nonMatching);
    }

    [Fact]
    public void Create_PreservesSelectedRulesAndCatalogMetadata()
    {
        var rule = new EngineeringRule { Id = "ARCH-001", Name = "Architecture", Category = "architecture", Severity = "CRITICAL" };
        var contract = EngineeringContractGenerator.Create(
            new ContractContext("Login", "android", "Kotlin", "dev", "mobile"),
            new[] { rule },
            "1.0");

        Assert.Equal("1.0", contract.CatalogSchemaVersion);
        Assert.Single(contract.Rules);
        Assert.Equal("ARCH-001", contract.Rules[0].Id);
    }

    [Fact]
    public void RuleCatalog_OrdersByCategoryThenSeverity()
    {
        var defaults = new RuleDefaults { Status = "active", Scope = new RuleScope() };
        var catalog = RuleCatalog.Create("1.0", defaults, new[]
        {
            new EngineeringRule { Id = "CICD-001", Name = "CI", Category = "cicd", Severity = "BLOCKER" },
            new EngineeringRule { Id = "SEC-001", Name = "Security", Category = "security", Severity = "LOW" },
            new EngineeringRule { Id = "ARCH-001", Name = "Architecture", Category = "architecture", Severity = "HIGH" }
        });

        Assert.Equal(new[] { "ARCH-001", "SEC-001", "CICD-001" }, catalog.Rules.Select(rule => rule.Id));
    }

    [Fact]
    public void Load_RequiresCatalogConfigurationAndLoadsRules()
    {
        var directory = Directory.CreateTempSubdirectory("enoc-rules-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "config.yaml"), """
                schema_version: "1.0"
                severity:
                  CRITICAL:
                    description: "Blocks delivery."
                enforcement_types:
                  AI:
                    description: "AI review."
                lifecycle:
                  states: ["active"]
                defaults:
                  version: "1.0"
                  status: "active"
                  scope:
                    platforms: ["android"]
                    project_types: ["mobile"]
                    environments: ["dev"]
                """);
            File.WriteAllText(Path.Combine(directory.FullName, "rules.yaml"), """
                rules:
                  - id: ARCH-001
                    name: Architecture
                    category: architecture
                    severity: CRITICAL
                    enforcement: [AI]
                """);

            var catalog = RuleCatalogLoader.Load(directory.FullName);

            Assert.Equal("1.0", catalog.SchemaVersion);
            Assert.Single(catalog.Rules);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Load_ParsesRepositoryCatalog()
    {
        var rulesDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../rules"));

        var catalog = RuleCatalogLoader.Load(rulesDirectory);

        Assert.Equal("2.0", catalog.SchemaVersion);
        Assert.Equal(27, catalog.Rules.Count);
    }

    [Fact]
    public void MarkdownOutput_EscapesTaskFormattingCharacters()
    {
        var contract = EngineeringContractGenerator.Create(
            new ContractContext("Create [secure] *login* #1", "android", "Kotlin", "dev", "mobile"),
            Array.Empty<EngineeringRule>());

        var markdown = EngineeringContractGenerator.ToMarkdown(contract);

        Assert.Contains("Create \\[secure\\] \\*login\\* \\#1", markdown);
        Assert.DoesNotContain("Create [secure] *login* #1", markdown);
    }

    [Fact]
    public void UserSettings_RoundTripSelectedRuleIds()
    {
        var settings = new UserSettings(
            "Login",
            "android",
            "dev",
            "markdown",
            new[] { "ARCH-001", "SEC-003" });
        var json = System.Text.Json.JsonSerializer.Serialize(settings);
        var restored = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(new[] { "ARCH-001", "SEC-003" }, restored!.SelectedRuleIds);
    }

    [Fact]
    public void RepositoryReview_FindsForbiddenPatternsAndSkipsBuildDirectories()
    {
        var directory = Directory.CreateTempSubdirectory("enoc-review-");
        try
        {
            Directory.CreateDirectory(Path.Combine(directory.FullName, "bin"));
            File.WriteAllText(Path.Combine(directory.FullName, "Main.kt"), "val token = \"hardcoded-secret\"");
            File.WriteAllText(Path.Combine(directory.FullName, "bin", "Generated.kt"), "val token = \"hardcoded-secret\"");
            var rule = new EngineeringRule
            {
                Id = "SEC-001",
                Name = "No secrets",
                Severity = "BLOCKER",
                Forbidden = new List<string> { "hardcoded-secret" }
            };

            var review = new RepositoryReviewService().Review(directory.FullName, new[] { rule });

            var finding = Assert.Single(review.Findings);
            Assert.Equal("Main.kt", finding.FilePath);
            Assert.Equal(1, finding.LineNumber);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void UserSettings_RoundTripReviewContext()
    {
        var settings = new UserSettings("Login", "android", "dev", "json", Array.Empty<string>())
        {
            Mode = "review",
            RepositoryPath = "/workspace/mobile-app"
        };

        var restored = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(
            System.Text.Json.JsonSerializer.Serialize(settings));

        Assert.NotNull(restored);
        Assert.Equal("review", restored!.Mode);
        Assert.Equal("/workspace/mobile-app", restored.RepositoryPath);
    }
}
