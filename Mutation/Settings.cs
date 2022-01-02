namespace Mutation;

internal class Settings
{
	internal AzureComputerVisionSettings AzureComputerVisionSettings { get; set; }
}

internal class AzureComputerVisionSettings
{
	internal string SubscriptionKey { get; set; }
	internal string Endpoint { get; set; }
}
