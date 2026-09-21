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

        Assert.Equal("1.0", catalog.SchemaVersion);
        Assert.Equal(25, catalog.Rules.Count);
    }
}
