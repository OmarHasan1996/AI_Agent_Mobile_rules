namespace AI_Enoc_Engineering_Agent;

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
