namespace Mutation;

public class Settings
{
	public string UserInstructions { get; set; }
	public AzureComputerVisionSettings AzureComputerVisionSettings { get; set; }
}

public class AzureComputerVisionSettings
{
	public string SubscriptionKey { get; set; }
	public string Endpoint { get; set; }
}
