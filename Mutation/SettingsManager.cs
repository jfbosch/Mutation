using CognitiveSupport;
using Newtonsoft.Json;
using OpenAI.ObjectModels;
using System.Diagnostics;
using System.Text;

namespace Mutation;

internal class SettingsManager
{
	private string SettingsFilePath { get; set; }
	private string SettingsFileFullPath => Path.GetFullPath(SettingsFilePath);

	public SettingsManager(
		string settingsFilePath)
	{
		SettingsFilePath = settingsFilePath;
	}

	internal Settings LoadAndEnsureSettings()
	{
		CreateSettingsFileOfNotExists(SettingsFileFullPath);

		string json = File.ReadAllText(SettingsFileFullPath);
		Settings settings = JsonConvert.DeserializeObject<Settings>(json);

		if (EnsureSettings(settings))
		{
			SaveSettingsToFile(settings);
		}

		return settings;
	}

	private void CreateSettingsFileOfNotExists(string fullPath)
	{
		if (!File.Exists(fullPath))
		{
			var settings = new Settings();
			EnsureSettings(settings);

			SaveSettingsToFile(settings);
			Process.Start("notepad.exe", SettingsFilePath);
		}
	}

	private bool EnsureSettings(Settings settings)
	{
		const string Placeholder = "<placeholder>";

		bool somethingWasMissing = false;

		settings.UserInstructions = "Change the values of the settings below to your preferences, save the file, and restart Mutation.exe. DeploymentId in the LlmSettings should be set to your Azure model Deployment Name.";

		if (settings.AzureComputerVisionSettings is null)
		{
			settings.AzureComputerVisionSettings = new AzureComputerVisionSettings();
			somethingWasMissing = true;
		}
		var azureComputerVisionSettings = settings.AzureComputerVisionSettings;
		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.OcrHotKey))
		{
			azureComputerVisionSettings.OcrHotKey = "ALT+J";
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.ScreenshotHotKey))
		{
			azureComputerVisionSettings.ScreenshotHotKey = "SHIFT+ALT+K";
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.ScreenshotOcrHotKey))
		{
			azureComputerVisionSettings.ScreenshotOcrHotKey = "SHIFT+ALT+J";
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.ApiKey))
		{
			azureComputerVisionSettings.ApiKey = Placeholder;
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.Endpoint))
		{
			azureComputerVisionSettings.Endpoint = Placeholder;
			somethingWasMissing = true;
		}


		//--------------------------------------
		if (settings.AudioSettings is null)
		{
			settings.AudioSettings = new AudioSettings();
			somethingWasMissing = true;
		}
		var audioSettings = settings.AudioSettings;
		if (string.IsNullOrWhiteSpace(audioSettings.MicrophoneToggleMuteHotKey))
		{
			audioSettings.MicrophoneToggleMuteHotKey = "ALT+Q";
			somethingWasMissing = true;
		}


		//----------------------------------
		if (settings.SpeetchToTextSettings is null)
		{
			settings.SpeetchToTextSettings = new SpeetchToTextSettings();
			somethingWasMissing = true;
		}
		var speechToTextSettings = settings.SpeetchToTextSettings;
		if (string.IsNullOrWhiteSpace(speechToTextSettings.SpeechToTextHotKey))
		{
			speechToTextSettings.SpeechToTextHotKey = "SHIFT+ALT+U";
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(speechToTextSettings.ApiKey))
		{
			speechToTextSettings.ApiKey = Placeholder;
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(speechToTextSettings.TempDirectory))
		{
			speechToTextSettings.TempDirectory = @"C:\Temp\Mutation";
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(speechToTextSettings.SpeechToTextPrompt))
		{
			speechToTextSettings.SpeechToTextPrompt = "Hello, let's use punctuation. Names: Kobus, Piro.";
			// This is optional, so we don't need to flag that something was missing.
		}


		//-------------------------------
		if (settings.LlmSettings is null)
		{
			settings.LlmSettings = new LlmSettings();
			somethingWasMissing = true;
		}
		var llmSettings = settings.LlmSettings;
		if (string.IsNullOrWhiteSpace(llmSettings.ApiKey))
		{
			llmSettings.ApiKey = Placeholder;
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(llmSettings.ResourceName))
		{
			llmSettings.ResourceName = "<The Azure resource name for your OpenAI service.>"; // Replace with your default value
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(llmSettings.FormatTranscriptPrompt))
		{
			llmSettings.FormatTranscriptPrompt = @"You are a helpful proofreader and editor. When you are asked to format a transcript, apply the following rules to improve the formatting of the text:
Replace the words 'new line' (case insensitive) with an actual new line character, and replace the words 'new paragraph' (case insensitive) with 2 new line characters, and replace the words 'new bullet' (case insensitive) with a newline character and a bullet character, eg. '- ', and end the preceding sentence with a full stop '.', and start the new sentence with a capital letter, and do not make any other changes.

Here is an example of a raw transcript and the reformatted text:

----- Transcript:
The radiology report - the written analysis by the radiologist interpreting your imaging study - is transmitted to the requesting physician or medical specialist new line the doctor or specialist will then relay the full analysis to you, along with recommendations and/or prescriptions. New paragraph Depending on the results, this might include new bullet scheduling further diagnostic tests new bullet initiating a new medication regimen new bullet recommending physical therapy new bullet or possibly even planning for a surgical intervention. New paragraph. Collaboration among various healthcare professionals ensures that the information gleaned from the radiology report is utilized to provide the most effective and individualized care tailored to your specific condition and needs. New line end of summary.


----- Reformatted Text:
The radiology report - the written analysis by the radiologist interpreting your imaging study - is transmitted to the requesting physician or medical specialist.
The doctor or specialist will then relay the full analysis to you, along with recommendations and/or prescriptions.

Depending on the results, this might include:
- scheduling further diagnostic tests,
- initiating a new medication regimen,
- recommending physical therapy,
- or possibly even planning for a surgical intervention.

Collaboration among various healthcare professionals ensures that the information gleaned from the radiology report is utilized to provide the most effective and individualized care tailored to your specific condition and needs.
End of summary.
";
			// No need to mark something as missing.
			//somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(llmSettings.ReviewTranscriptPrompt))
		{
			llmSettings.ReviewTranscriptPrompt = @"You are an expert medical doctor specialising in radiology.
When you are asked to review a radiological report, you should do the following:

Check for spelling mistakes, and Grammer mistakes.


Check for logical consistency through out the report, and specifically between the body and the conclusion or comment. E.g. if the report body indicates a tumour was present, the conclusion or comment should not contradict that. Likewise, measurements in the body of the report and the comment or conclusion should be consistent.

Additionally, specifically look out for mistakes common to audio transcriptions.
- Similar sounding words (Hypointense vs hyperintense, hypo- vs hyperdense, etc)
- Leaving out short vital words (eg. ‘no nodules’ transcribed as 'nodules')
- Using left and right incorrectly/interchangeably.
- Contradictory statements (normal heart in the body of the report with cardiomegaly in the comment or conclusion)

You should provide your feedback without preamble, in bullet form, where each finding or problem is listed as a bullet point. If you find no issues or concerns, just say, ‘Review did not detect any issues.’
";
			// No need to mark something as missing.
			//somethingWasMissing = true;
		}


		if (llmSettings.ModelDeploymentIdMaps == null || !llmSettings.ModelDeploymentIdMaps.Any())
		{
			llmSettings.ModelDeploymentIdMaps = new List<LlmSettings.ModelDeploymentIdMap>
			{
				new LlmSettings.ModelDeploymentIdMap
				{
					ModelName = Models.Gpt_3_5_Turbo,
					DeploymentId = "gpt-35-turbo"
				},
				new LlmSettings.ModelDeploymentIdMap
				{
					ModelName = Models.Gpt_4,
					DeploymentId = "gpt-4"
				},
			};
			somethingWasMissing = true;
		}

		return somethingWasMissing;
	}

	public void SaveSettingsToFile(Settings settings)
	{
		string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
		File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
	}
}
