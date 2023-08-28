using CognitiveSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OpenAI.ObjectModels;
using System.Diagnostics;
using System.Text;

namespace Mutation;

internal class SettingsManager
{
	private static readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
	{
		Converters = new List<JsonConverter> { new StringEnumConverter() }
	};

	private string SettingsFilePath { get; set; }
	private string SettingsFileFullPath => Path.GetFullPath(SettingsFilePath);

	public SettingsManager(
		string settingsFilePath)
	{
		SettingsFilePath = settingsFilePath;
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
			llmSettings.ReviewTranscriptPrompt = @"You are an expert medical AI assistant; your task is to help doctors specialising in radiology.

When you are asked to review a radiological report, you should do the following:

1) Check for spelling and grammar mistakes. (Semicolons “;” are to be followed with a small letter word, unless the word is a proper noun or acronym.)
2) Check for logical consistency through out the report, and specifically between the body, or findings section, and the conclusion or comment.
- Example 1: If the report body indicates a tumour was present, the conclusion/comment should not contradict that.
- Example 2: ‘Normal heart’ in the body, or findings, of the report with ‘cardiomegaly’ in the conclusion/ comment, is a logical contradiction.
- Example 3: Using left and right incorrectly/interchangeably.
- Example 4: Measurements in the body or findings section of the report and the comment/conclusion should be consistent.
3) Specifically look out for mistakes common to audio transcriptions. E.g.
- Similar sounding words (Hypointense vs hyperintense, hypo- vs hyperdense, etc)
4) You should provide your feedback without preamble, in bullet form, and only if an issue was actually detected; where each issue found is listed as a bullet point; each bullet should be in the form: “- <Issue type>: <correction instruction>”.
Example issue 1: 
- Clarification: Change “RVD non-reactive” to “HIV non-reactive” to avoid confusion.
Example issue 2: 
- Spelling: Change ""subcentimeter hypodencities"" to ""subcentimeter hypodensities.""
Example issue 3: 
- Contradiction: Remove ""Bone fractures evident in left tibia"" from the comment, as it contradicts ""Normal bones"" in the body of the report.
Example issue 4: 
- Grammar: Change ""Additional low-density para-aortic pelvic and, inguinal lymphadenopathy."" to ""Additional low-density para-aortic, pelvic, and inguinal lymphadenopathy.""; to remove the misplaced comma after ""and"" to properly format the list.

When you are asked to apply revision corrections, you should do the following:

1)  For each of the correction instructions provided, apply the specific correction in question to the original transcript exactly as per the instruction.
2) If you are unable to apply the correction for any reason, add a note explaining why at the bottom of the transcript under a heading Review Feedback.
3) Besides the specific correction instructions, don’t make any other changes to the original transcript.
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

			// no need to flag as we set defaults.
			//somethingWasMissing = true;
		}

		if (llmSettings.TranscriptFormatRules == null || !llmSettings.TranscriptFormatRules.Any())
		{
			llmSettings.TranscriptFormatRules = new List<LlmSettings.TranscriptFormatRule>
			{
				new LlmSettings.TranscriptFormatRule
				{
					Find= "new line",
					ReplaceWith= $"{Environment.NewLine}",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "next line",
					ReplaceWith= $"{Environment.NewLine}",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "new paragraph",
					ReplaceWith= $"{Environment.NewLine}{Environment.NewLine}",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "next paragraph",
					ReplaceWith= $"{Environment.NewLine}{Environment.NewLine}",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "new bullet",
					ReplaceWith= $"{Environment.NewLine}- ",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "next bullet",
					ReplaceWith= $"{Environment.NewLine}- ",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "new colon",
					ReplaceWith= $": ",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "semicolon",
					ReplaceWith= $"; ",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "full stop",
					ReplaceWith= $". ",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "comma",
					ReplaceWith= $", ",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "exclamation mark",
					ReplaceWith= $"! ",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "question mark",
					ReplaceWith= $"? ",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "elipsis",
					ReplaceWith= $"... ",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},
				new LlmSettings.TranscriptFormatRule
				{
					Find= "dot dot dot",
					ReplaceWith= $"... ",
					CaseSensitive = false,
					MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart,
				},


			};

			// No need to flag something as missing as we set defaults.
			//somethingWasMissing = true;
		}

		return somethingWasMissing;
	}

	public void UpgradeSettings()
	{
		string json = File.ReadAllText(SettingsFileFullPath);

		Settings settings = JsonConvert.DeserializeObject<Settings>(json, _jsonSerializerSettings);

		if (settings.LlmSettings?.TranscriptFormatRules?.Count == 4)
		{
			// Get rid of the old defaults.
			settings.LlmSettings.TranscriptFormatRules.Clear();
		}

		SaveSettingsToFile(settings);
	}

	public void SaveSettingsToFile(Settings settings)
	{
		string json = JsonConvert.SerializeObject(settings, Formatting.Indented, _jsonSerializerSettings);
		File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
	}

	internal Settings LoadAndEnsureSettings()
	{
		CreateSettingsFileOfNotExists(SettingsFileFullPath);

		UpgradeSettings();

		string json = File.ReadAllText(SettingsFileFullPath);
		Settings settings = JsonConvert.DeserializeObject<Settings>(json, _jsonSerializerSettings);

		if (EnsureSettings(settings))
		{
			SaveSettingsToFile(settings);
		}

		return settings;
	}

}
