namespace AI_Enoc_Engineering_Agent;

using System.Text.Json;

public interface IRuleCatalogService
{
    RuleCatalog Load(string rulesDirectory);
}

public interface IContractService
{
    EngineeringContract Generate(ContractContext context, IReadOnlyList<EngineeringRule> rules, string catalogSchemaVersion);
    string Render(EngineeringContract contract, string format);
}

public sealed class RuleCatalogService : IRuleCatalogService
{
    public RuleCatalog Load(string rulesDirectory) => RuleCatalogLoader.Load(rulesDirectory);
}

public sealed class ContractService : IContractService
{
    public EngineeringContract Generate(ContractContext context, IReadOnlyList<EngineeringRule> rules, string catalogSchemaVersion) =>
        EngineeringContractGenerator.Create(context, rules, catalogSchemaVersion);

    public string Render(EngineeringContract contract, string format) =>
        format.Equals("json", StringComparison.OrdinalIgnoreCase)
            ? EngineeringContractGenerator.ToJson(contract)
            : EngineeringContractGenerator.ToMarkdown(contract);
}

public sealed record UserSettings(
    string Task,
    string Platform,
    string Environment,
    string Format,
    IReadOnlySet<string> SelectedRuleIds);

public interface IUserSettingsService
{
    UserSettings Load();
    void Save(UserSettings settings);
}

public sealed class UserSettingsService : IUserSettingsService
{
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ENOC",
        "settings.json");

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return Defaults();

            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(_settingsPath));
            return settings ?? Defaults();
        }
        catch (JsonException)
        {
            return Defaults();
        }
        catch (IOException)
        {
            return Defaults();
        }
    }

    public void Save(UserSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (directory is null)
            return;

        Directory.CreateDirectory(directory);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static UserSettings Defaults() =>
        new(string.Empty, "android", "dev", "markdown", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
