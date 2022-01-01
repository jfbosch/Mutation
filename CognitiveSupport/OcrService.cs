// See https://aka.ms/new-console-template for more information
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using System.Text;

namespace CognitiveSupport;

public class OcrService
{
	//TODO: move to config
	string subscriptionKey = "674d788a168941dd887fe5674f3a3110";
	string endpoint = "https://bo-computer-vission-01.cognitiveservices.azure.com/";


	private ComputerVisionClient client;


	public OcrService(string subscriptionKey, string endpoint)
	{
		//this.subscriptionKey = subscriptionKey ?? throw new ArgumentNullException(nameof(subscriptionKey));
		//this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

		client = Authenticate(endpoint, subscriptionKey);
	}

	public string ExtractText()
	{
		string path = @"C:\Temp\nord.jpg";
		return ReadFileUrl(client, path).Result;
	}


	/*
	 * AUTHENTICATE
	 * Creates a Computer Vision client used by each example.
	 */
	ComputerVisionClient Authenticate(string endpoint, string key)
	{
		ComputerVisionClient client =
			new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
			{
				Endpoint = endpoint
			};
		return client;
	}


	/*
	 * READ FILE - URL 
	 * Extracts text. 
	 */
	async Task<string> ReadFileUrl(
		ComputerVisionClient client,
		string filePath)
	{
		Console.WriteLine("----------------------------------------------------------");
		Console.WriteLine("READ FROM file");
		Console.WriteLine(filePath);

		using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		var textHeaders = await client.ReadInStreamAsync(fileStream);
		// After the request, get the operation location (operation ID)
		string operationLocation = textHeaders.OperationLocation;
		Thread.Sleep(500);
		Console.WriteLine($"operationLocation {operationLocation}");

		// Retrieve the URI where the extracted text will be stored from the Operation-Location header.
		// We only need the ID and not the full URL
		const int numberOfCharsInOperationId = 36;
		string operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);

		// Extract the text
		ReadOperationResult results;
		Console.WriteLine($"Extracting text from URL file {Path.GetFileName(filePath)}...");
		Console.WriteLine();
		do
		{
			results = await client.GetReadResultAsync(Guid.Parse(operationId));
		}
		while ((results.Status == OperationStatusCodes.Running
			|| results.Status == OperationStatusCodes.NotStarted));

		// Display the found text.
		Console.WriteLine();
		StringBuilder sb = new();
		var textUrlFileResults = results.AnalyzeResult.ReadResults;
		foreach (ReadResult page in textUrlFileResults)
		{
			foreach (Line line in page.Lines)
			{
				Console.WriteLine(line.Text);
				sb.AppendLine(line.Text);
			}
		}
		Console.WriteLine();
		return sb.ToString();
	}

}


