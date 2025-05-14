using CognitiveSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using OpenAI.ObjectModels;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace Mutation;

internal class SettingsManager : ISettingsManager
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
		const string PlaceholderValue = "<placeholder>";
		const string PlaceholderUrl = "https://placeholder.com";

		bool somethingWasMissing = false;

		settings.UserInstructions = "Change the values of the settings below to your preferences, save the file, and restart Mutation.exe. DeploymentId in the LlmSettings should be set to your Azure model Deployment Name.";

		if (settings.AzureComputerVisionSettings is null)
		{
			settings.AzureComputerVisionSettings = new AzureComputerVisionSettings();
			somethingWasMissing = true;
		}
		var azureComputerVisionSettings = settings.AzureComputerVisionSettings;

		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.ScreenshotHotKey))
		{
			azureComputerVisionSettings.ScreenshotHotKey = "SHIFT+ALT+K";
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.OcrHotKey))
		{
			azureComputerVisionSettings.OcrHotKey = "ALT+J";
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.ScreenshotOcrHotKey))
		{
			azureComputerVisionSettings.ScreenshotOcrHotKey = "SHIFT+ALT+J";
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.OcrLeftToRightTopToBottomHotKey))
		{
			azureComputerVisionSettings.OcrLeftToRightTopToBottomHotKey = "ALT+K";
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.ScreenshotLeftToRightTopToBottomOcrHotKey))
		{
			azureComputerVisionSettings.ScreenshotLeftToRightTopToBottomOcrHotKey = "SHIFT+ALT+E";
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.ApiKey))
		{
			azureComputerVisionSettings.ApiKey = PlaceholderValue;
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.Endpoint))
		{
			azureComputerVisionSettings.Endpoint = PlaceholderUrl;
			somethingWasMissing = true;
		}


		//--------------------------------------
		if (settings.AudioSettings is null)
		{
			settings.AudioSettings = new AudioSettings();
			somethingWasMissing = true;
		}
		var audioSettings = settings.AudioSettings;
		if ( string.IsNullOrWhiteSpace ( audioSettings.MicrophoneToggleMuteHotKey ) )
		{
			audioSettings.MicrophoneToggleMuteHotKey = "ALT+Q";
			somethingWasMissing = true;
		}
		if ( audioSettings.CustomBeepSettings == null )
		{
			audioSettings.CustomBeepSettings = new AudioSettings.CustomBeepSettingsData ( );
			somethingWasMissing = true;
		}
		if ( audioSettings.CustomBeepSettings.UseCustomBeeps )
		{
			string[] allowedExtensions = new[] { ".wav" };
			var beepIssues = new List<string>();

			string successPath = audioSettings.CustomBeepSettings?.BeepSuccessFile ?? string.Empty;
			if ( string.IsNullOrWhiteSpace ( successPath ) ||
				!allowedExtensions.Contains ( Path.GetExtension ( successPath )?.ToLower ( ) ) ||
				!File.Exists ( successPath ) )
			{
				somethingWasMissing = true;

				if ( !string.IsNullOrWhiteSpace ( successPath ) && Path.GetExtension ( successPath )?.ToLower ( ) != ".wav" )
					beepIssues.Add ( $"Custom success beep must be a .wav file: {successPath}" );
				else
					beepIssues.Add ( $"Could not load success beep file: {successPath}" );
			}

			string failurePath = audioSettings.CustomBeepSettings?.BeepFailureFile ?? string.Empty;
			if ( string.IsNullOrWhiteSpace ( failurePath ) ||
				!allowedExtensions.Contains ( Path.GetExtension ( failurePath )?.ToLower ( ) ) ||
				!File.Exists ( failurePath ) )
			{
				somethingWasMissing = true;

				if ( !string.IsNullOrWhiteSpace ( failurePath ) && Path.GetExtension ( failurePath )?.ToLower ( ) != ".wav" )
					beepIssues.Add ( $"Custom failure beep must be a .wav file: {failurePath}" );
				else
					beepIssues.Add ( $"Could not load failure beep file: {failurePath}" );
			}

			string startPath = audioSettings.CustomBeepSettings?.BeepStartFile ?? string.Empty;
			if ( string.IsNullOrWhiteSpace ( startPath ) ||
				!allowedExtensions.Contains ( Path.GetExtension ( startPath )?.ToLower ( ) ) ||
				!File.Exists ( startPath ) )
			{
				somethingWasMissing = true;

				if ( !string.IsNullOrWhiteSpace ( startPath ) && Path.GetExtension ( startPath )?.ToLower ( ) != ".wav" )
					beepIssues.Add ( $"Custom start beep must be a .wav file: {startPath}" );
				else
					beepIssues.Add ( $"Could not load start beep file: {startPath}" );
			}

			string endPath = audioSettings.CustomBeepSettings?.BeepEndFile ?? string.Empty;
			if ( string.IsNullOrWhiteSpace ( endPath ) ||
				!allowedExtensions.Contains ( Path.GetExtension ( endPath )?.ToLower ( ) ) ||
				!File.Exists ( endPath ) )
			{
				somethingWasMissing = true;

				if ( !string.IsNullOrWhiteSpace ( endPath ) && Path.GetExtension ( endPath )?.ToLower ( ) != ".wav" )
					beepIssues.Add ( $"Custom end beep must be a .wav file: {endPath}" );
				else
					beepIssues.Add ( $"Could not load end beep file: {endPath}" );
			}

			string mutePath = audioSettings.CustomBeepSettings?.BeepMuteFile ?? string.Empty;
			if ( string.IsNullOrWhiteSpace ( mutePath ) ||
				!allowedExtensions.Contains ( Path.GetExtension ( mutePath )?.ToLower ( ) ) ||
				!File.Exists ( mutePath ) )
			{
				somethingWasMissing = true;

				if ( !string.IsNullOrWhiteSpace ( mutePath ) && Path.GetExtension ( mutePath )?.ToLower ( ) != ".wav" )
					beepIssues.Add ( $"Custom mute beep must be a .wav file: {mutePath}" );
				else
					beepIssues.Add ( $"Could not load mute beep file: {mutePath}" );
			}

			string unmutePath = audioSettings.CustomBeepSettings?.BeepUnmuteFile ?? string.Empty;
			if ( string.IsNullOrWhiteSpace ( unmutePath ) ||
				!allowedExtensions.Contains ( Path.GetExtension ( unmutePath )?.ToLower ( ) ) ||
				!File.Exists ( unmutePath ) )
			{
				somethingWasMissing = true;

				if ( !string.IsNullOrWhiteSpace ( unmutePath ) && Path.GetExtension ( unmutePath )?.ToLower ( ) != ".wav" )
					beepIssues.Add ( $"Custom unmute beep must be a .wav file: {unmutePath}" );
				else
					beepIssues.Add ( $"Could not load unmute beep file: {unmutePath}" );
			}

			if ( beepIssues.Any ( ) )
			{
				if (audioSettings.CustomBeepSettings != null)
					audioSettings.CustomBeepSettings.UseCustomBeeps = false;

				string message =
					"The following issues were found with the custom beep settings:" + Environment.NewLine + Environment.NewLine +
					string.Join(Environment.NewLine, beepIssues) + Environment.NewLine + Environment.NewLine +
					"Falling back to default beep sounds. UseCustomBeeps has been disabled." + Environment.NewLine +  Environment.NewLine +
					"To use custom beeps again, fix the issues above and re-enable UseCustomBeeps in the settings file.";

				System.Windows.Forms.MessageBox.Show (
					message,
					"Custom Beep Settings Issues",
					System.Windows.Forms.MessageBoxButtons.OK,
					System.Windows.Forms.MessageBoxIcon.Warning
				);
			}
		}


		//----------------------------------
		if ( settings.SpeetchToTextSettings is null)
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
		if (speechToTextSettings.Services is null)
		{
			speechToTextSettings.Services = new SpeetchToTextServiceSettings[] { };
			somethingWasMissing = true;
		}
		if (!speechToTextSettings.Services.Any())
		{
			speechToTextSettings.ActiveSpeetchToTextService = "OpenAI Whisper 1";
			SpeetchToTextServiceSettings service = new SpeetchToTextServiceSettings
			{
				Name = speechToTextSettings.ActiveSpeetchToTextService,
				Provider = SpeechToTextProviders.OpenAi,
				ModelId = "whisper-1",
				BaseDomain = "https://api.openai.com/",
			};
			speechToTextSettings.Services = speechToTextSettings.Services.Append(service).ToArray();
			somethingWasMissing = true;
		}
		foreach (var s in speechToTextSettings.Services)
		{
			if (s.Provider == SpeechToTextProviders.None)
				s.Provider = SpeechToTextProviders.OpenAi;
			if (string.IsNullOrWhiteSpace(s.ApiKey))
			{
				s.ApiKey = PlaceholderValue;
				somethingWasMissing = true;
			}
			if (string.IsNullOrWhiteSpace(s.SpeechToTextPrompt))
			{
				s.SpeechToTextPrompt = "Hello, let's use punctuation. Names: Kobus, Piro.";
				// This is optional, so we don't need to flag that something was missing.
				//somethingWasMissing = true;
			}
			if (s.TimeoutSeconds <= 0)
			{
				s.TimeoutSeconds = 10;
				somethingWasMissing = true;
			}
		}

		if (string.IsNullOrWhiteSpace(speechToTextSettings.SpeechToTextHotKey))
		{
			speechToTextSettings.SpeechToTextHotKey = "SHIFT+ALT+U";
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(speechToTextSettings.TempDirectory))
		{
			speechToTextSettings.TempDirectory = @"C:\Temp\Mutation";
			somethingWasMissing = true;
		}

		var duplicateNames = speechToTextSettings.Services
			 .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
			 .Where(g => g.Count() > 1)
			 .Select(g => g.Key)
			 .ToArray();
		if (duplicateNames.Any())
		{
			throw new InvalidOperationException(
				 $"Duplicate service names found in SpeetchToTextSettings.Services: {string.Join(", ", duplicateNames)}. " +
				 "Please ensure each speech-to-text service has a unique name to avoid conflicts.");
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
			llmSettings.ApiKey = PlaceholderValue;
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(llmSettings.ResourceName))
		{
			llmSettings.ResourceName = "The-Azure-resource-name-for-your-OpenAI-service";
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(llmSettings.FormatTranscriptPrompt))
		{
			//TODO: formatting is now done with normal code, so this should be deleted.

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
					Find= "newline",
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
					Find= "new paragraphs",
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

		//----------------------------------
		if (settings.TextToSpeechSettings is null)
		{
			settings.TextToSpeechSettings = new TextToSpeechSettings();
			somethingWasMissing = true;
		}
		var textToSpeechSettings = settings.TextToSpeechSettings;
		if (string.IsNullOrWhiteSpace(textToSpeechSettings.TextToSpeechHotKey))
		{
			textToSpeechSettings.TextToSpeechHotKey = "CTRL+SHIFT+Q";
			somethingWasMissing = true;
		}

		if (settings.HotKeyRouterSettings is null || !settings.HotKeyRouterSettings.Mappings.Any())
		{
			settings.HotKeyRouterSettings = new();
			// Add a sample hotkey router mapping.
			settings.HotKeyRouterSettings.Mappings.Add
			(
				new HotKeyRouterSettings.HotKeyRouterMap("CONTROL+SHIFT+ALT+8", "CONTROL+SHIFT+ALT+9")
			);
		}

		return somethingWasMissing;
	}

	public void UpgradeSettings()
	{
		string json = File.ReadAllText(SettingsFileFullPath);

		JObject jObj = JObject.Parse(json);

		if (!(jObj["SpeetchToTextSettings"] is JObject settings))
			return;

		// Check if the JSON is already in the desired format.
		// Here, we assume correctness if a "Services" property (as an array) exists.
		if (settings["Services"] == null || settings["Services"].Type != JTokenType.Array)
		{
			string providerName = settings.Value<string>("Service") ?? "";

			// Create the new service object and migrate the relevant properties.
			JObject serviceObj = new JObject
			{
				["Name"] = providerName,             // Use the provider name as the service Name.
				["Provider"] = providerName,         // Also set the Provider.
				["ApiKey"] = settings["ApiKey"],
				["BaseDomain"] = settings["BaseDomain"],
				["ModelId"] = settings["ModelId"],
				["SpeechToTextPrompt"] = settings["SpeechToTextPrompt"]
			};

			// Create a new services array with the service object as the first element.
			JArray servicesArray = new JArray { serviceObj };

			// Remove the migrated properties from the root of SpeetchToTextSettings.
			settings.Remove("Service");
			settings.Remove("ApiKey");
			settings.Remove("BaseDomain");
			settings.Remove("ModelId");
			settings.Remove("SpeechToTextPrompt");

			// Add the new properties: the active service and the services array.
			settings["ActiveSpeetchToTextService"] = providerName;
			settings["Services"] = servicesArray;
		}

		foreach (var service in settings["Services"])
		{
			if (service["Provider"]?.ToString() == "OpenAiWhisper")
				service["Provider"] = "OpenAi";
		}

		File.WriteAllText(SettingsFileFullPath, jObj.ToString(Formatting.Indented), Encoding.UTF8);
	}

	public void SaveSettingsToFile(Settings settings)
	{
		string json = JsonConvert.SerializeObject(settings, Formatting.Indented, _jsonSerializerSettings);
		File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
	}

	public Settings LoadAndEnsureSettings()
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
