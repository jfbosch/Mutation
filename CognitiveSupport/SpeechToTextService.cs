namespace CognitiveSupport
{
	public class SpeechToTextService
	{
		private readonly string ApiKey;
		private readonly string Endpoint;
		private readonly object _lock = new object();

		public SpeechToTextService(
			string apiKey,
			string endpoint)
		{
			ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
			Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
		}

		public async Task<string> ConvertAudioToText(
			string filePath)
		{
			return "xx";
		}
	}
}