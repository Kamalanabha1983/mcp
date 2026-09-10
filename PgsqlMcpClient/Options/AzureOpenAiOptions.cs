namespace PgsqlMcpClient.Options;

public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    public bool UseManagedIdentity { get; set; } = true;

    public string ApiKey { get; set; } = string.Empty;
}