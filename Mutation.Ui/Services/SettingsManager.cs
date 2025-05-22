using CognitiveSupport;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using CognitiveSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using OpenAI.ObjectModels;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Mutation.Ui.Services;

internal class SettingsManager : ISettingsManager
{
	private static readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
	{
		Converters = new List<JsonConverter> { new StringEnumConverter() }
	};
	private static readonly string PlaceholderValue = "<placeholder>";
	private static readonly string PlaceholderUrl = "https://placeholder.com";

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
			EnsureSettings(settings); // EnsureSettings will now populate with defaults

			SaveSettingsToFile(settings);
			// It's good practice to inform the user that a new settings file was created and needs configuration.
			// Consider logging this or showing a message box if this is a GUI application.
			// For now, opening in notepad is a direct way to show the user.
			Process.Start("notepad.exe", SettingsFileFullPath); // Use SettingsFileFullPath for consistency
		}
	}

	private bool EnsureSettings(Settings settings)
	{
		bool modified = false;

		settings.UserInstructions = "Change the values of the settings below to your preferences, save the file, and restart Mutation.exe. DeploymentId in the LlmSettings should be set to your Azure model Deployment Name.";

		if (settings.AzureComputerVisionSettings is null)
		{
			settings.AzureComputerVisionSettings = new AzureComputerVisionSettings();
			modified = true;
		}
		modified |= EnsureAzureComputerVisionSettings(settings.AzureComputerVisionSettings);

		if (settings.AudioSettings is null)
		{
			settings.AudioSettings = new AudioSettings();
			modified = true;
		}
		modified |= EnsureAudioSettings(settings.AudioSettings);

		if (settings.SpeetchToTextSettings is null)
		{
			settings.SpeetchToTextSettings = new SpeetchToTextSettings();
			modified = true;
		}
		modified |= EnsureSpeetchToTextSettings(settings.SpeetchToTextSettings);

		if (settings.LlmSettings is null)
		{
			settings.LlmSettings = new LlmSettings();
			modified = true;
		}
		modified |= EnsureLlmSettings(settings.LlmSettings);

		if (settings.TextToSpeechSettings is null)
		{
			settings.TextToSpeechSettings = new TextToSpeechSettings();
			modified = true;
		}
		modified |= EnsureTextToSpeechSettings(settings.TextToSpeechSettings);
		
		if (settings.HotKeyRouterSettings is null)
		{
			settings.HotKeyRouterSettings = new HotKeyRouterSettings();
			modified = true;
		}
		modified |= EnsureHotKeyRouterSettings(settings.HotKeyRouterSettings);

		return modified;
	}

	private bool EnsureAzureComputerVisionSettings(AzureComputerVisionSettings settings)
	{
		bool modified = false;
		if (settings.TimeoutSeconds <= 0)
		{
			settings.TimeoutSeconds = 10;
			// Assuming changing a value from invalid to default counts as modification for saving.
			// If it was 0 and became 10, it's a change.
			modified = true; 
		}

		if (string.IsNullOrWhiteSpace(settings.ScreenshotHotKey))
		{
			settings.ScreenshotHotKey = "SHIFT+ALT+K";
			modified = true;
		}
		if (string.IsNullOrWhiteSpace(settings.OcrHotKey))
		{
			settings.OcrHotKey = "ALT+J";
			modified = true;
		}
		if (string.IsNullOrWhiteSpace(settings.ScreenshotOcrHotKey))
		{
			settings.ScreenshotOcrHotKey = "SHIFT+ALT+J";
			modified = true;
		}
		if (string.IsNullOrWhiteSpace(settings.OcrLeftToRightTopToBottomHotKey))
		{
			settings.OcrLeftToRightTopToBottomHotKey = "ALT+K";
			modified = true;
		}
		if (string.IsNullOrWhiteSpace(settings.ScreenshotLeftToRightTopToBottomOcrHotKey))
		{
			settings.ScreenshotLeftToRightTopToBottomOcrHotKey = "SHIFT+ALT+E";
			modified = true;
		}
		if (string.IsNullOrWhiteSpace(settings.ApiKey))
		{
			settings.ApiKey = PlaceholderValue;
			modified = true;
		}
		if (string.IsNullOrWhiteSpace(settings.Endpoint))
		{
			settings.Endpoint = PlaceholderUrl;
			modified = true;
		}
		return modified;
	}

	private bool EnsureAudioSettings(AudioSettings settings)
	{
		bool modified = false;
		if (string.IsNullOrWhiteSpace(settings.MicrophoneToggleMuteHotKey))
		{
			settings.MicrophoneToggleMuteHotKey = "ALT+Q";
			modified = true;
		}
		if (settings.CustomBeepSettings == null)
		{
			settings.CustomBeepSettings = new AudioSettings.CustomBeepSettingsData();
			modified = true;
		}

		// This part is tricky because `somethingWasMissing` was set inside the conditions.
		// The original logic set `somethingWasMissing = true` if a file was invalid/missing,
		// and then potentially set `UseCustomBeeps = false`.
		// If `UseCustomBeeps` was already true, and we find issues, we set it to false.
		// This change (true -> false) should be considered a modification.
		// If `UseCustomBeeps` was false, this block is skipped, no modification.
		if (settings.CustomBeepSettings.UseCustomBeeps)
		{
			string[] allowedExtensions = new[] { ".wav" };
			var beepIssues = new List<string>();
			bool originalUseCustomBeeps = settings.CustomBeepSettings.UseCustomBeeps;

			string successPath = settings.CustomBeepSettings?.BeepSuccessFile ?? string.Empty;
			if (string.IsNullOrWhiteSpace(successPath) || !allowedExtensions.Contains(Path.GetExtension(successPath)?.ToLower()) || !File.Exists(successPath))
			{
				if (!string.IsNullOrWhiteSpace(successPath) && Path.GetExtension(successPath)?.ToLower() != ".wav") beepIssues.Add($"Custom success beep must be a .wav file: {successPath}");
				else beepIssues.Add($"Could not load success beep file: {successPath}");
			}

			string failurePath = settings.CustomBeepSettings?.BeepFailureFile ?? string.Empty;
			if (string.IsNullOrWhiteSpace(failurePath) || !allowedExtensions.Contains(Path.GetExtension(failurePath)?.ToLower()) || !File.Exists(failurePath))
			{
				if (!string.IsNullOrWhiteSpace(failurePath) && Path.GetExtension(failurePath)?.ToLower() != ".wav") beepIssues.Add($"Custom failure beep must be a .wav file: {failurePath}");
				else beepIssues.Add($"Could not load failure beep file: {failurePath}");
			}

			string startPath = settings.CustomBeepSettings?.BeepStartFile ?? string.Empty;
			if (string.IsNullOrWhiteSpace(startPath) || !allowedExtensions.Contains(Path.GetExtension(startPath)?.ToLower()) || !File.Exists(startPath))
			{
				if (!string.IsNullOrWhiteSpace(startPath) && Path.GetExtension(startPath)?.ToLower() != ".wav") beepIssues.Add($"Custom start beep must be a .wav file: {startPath}");
				else beepIssues.Add($"Could not load start beep file: {startPath}");
			}

			string endPath = settings.CustomBeepSettings?.BeepEndFile ?? string.Empty;
			if (string.IsNullOrWhiteSpace(endPath) || !allowedExtensions.Contains(Path.GetExtension(endPath)?.ToLower()) || !File.Exists(endPath))
			{
				if (!string.IsNullOrWhiteSpace(endPath) && Path.GetExtension(endPath)?.ToLower() != ".wav") beepIssues.Add($"Custom end beep must be a .wav file: {endPath}");
				else beepIssues.Add($"Could not load end beep file: {endPath}");
			}

			string mutePath = settings.CustomBeepSettings?.BeepMuteFile ?? string.Empty;
			if (string.IsNullOrWhiteSpace(mutePath) || !allowedExtensions.Contains(Path.GetExtension(mutePath)?.ToLower()) || !File.Exists(mutePath))
			{
				if (!string.IsNullOrWhiteSpace(mutePath) && Path.GetExtension(mutePath)?.ToLower() != ".wav") beepIssues.Add($"Custom mute beep must be a .wav file: {mutePath}");
				else beepIssues.Add($"Could not load mute beep file: {mutePath}");
			}

			string unmutePath = settings.CustomBeepSettings?.BeepUnmuteFile ?? string.Empty;
			if (string.IsNullOrWhiteSpace(unmutePath) || !allowedExtensions.Contains(Path.GetExtension(unmutePath)?.ToLower()) || !File.Exists(unmutePath))
			{
				if (!string.IsNullOrWhiteSpace(unmutePath) && Path.GetExtension(unmutePath)?.ToLower() != ".wav") beepIssues.Add($"Custom unmute beep must be a .wav file: {unmutePath}");
				else beepIssues.Add($"Could not load unmute beep file: {unmutePath}");
			}

			if (beepIssues.Any())
			{
				// This path implies an issue was found with one of the beep files.
				// The original code set `somethingWasMissing = true` for each problematic file.
				// We should ensure `modified = true` if `UseCustomBeeps` is changed to `false`.
				if (settings.CustomBeepSettings != null) // Should not be null due to check above
				{
					settings.CustomBeepSettings.UseCustomBeeps = false;
					if (originalUseCustomBeeps) // If it was true and now is false, it's a modification.
					{
						modified = true;
					}
				}
				
				// The MessageBox logic is a side effect and doesn't directly contribute to `modified` status,
				// beyond the fact that `UseCustomBeeps` might be changed.
				string message = "The following issues were found with the custom beep settings:" + Environment.NewLine + Environment.NewLine +
								 string.Join(Environment.NewLine, beepIssues) + Environment.NewLine + Environment.NewLine +
								 "Falling back to default beep sounds. UseCustomBeeps has been disabled." + Environment.NewLine + Environment.NewLine +
								 "To use custom beeps again, fix the issues above and re-enable UseCustomBeeps in the settings file.";
				System.Windows.Forms.MessageBox.Show(message, "Custom Beep Settings Issues", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
			}
		}
		return modified;
	}

	private bool EnsureSpeetchToTextSettings(SpeetchToTextSettings settings)
	{
		bool modified = false;
		if (string.IsNullOrWhiteSpace(settings.SpeechToTextHotKey))
		{
			settings.SpeechToTextHotKey = "SHIFT+ALT+U";
			modified = true;
		}
		if (settings.Services is null)
		{
			settings.Services = Array.Empty<SpeetchToTextServiceSettings>();
			modified = true;
		}
		if (!settings.Services.Any())
		{
			settings.ActiveSpeetchToTextService = "OpenAI Whisper 1";
			SpeetchToTextServiceSettings service = new SpeetchToTextServiceSettings
			{
				Name = settings.ActiveSpeetchToTextService,
				Provider = SpeechToTextProviders.OpenAi,
				ModelId = "whisper-1",
				BaseDomain = "https://api.openai.com/",
				ApiKey = PlaceholderValue, // Default new service should also get placeholder
				SpeechToTextPrompt = "Hello, let's use punctuation. Names: Kobus, Piro.", // Default prompt
				TimeoutSeconds = 10 // Default timeout
			};
			settings.Services = settings.Services.Append(service).ToArray();
			modified = true;
		}
		else // if services exist, iterate and check them
		{
			foreach (var s in settings.Services)
			{
				if (s.Provider == SpeechToTextProviders.None)
				{
					s.Provider = SpeechToTextProviders.OpenAi;
					modified = true; // Changing provider from None to OpenAi is a modification
				}
				if (string.IsNullOrWhiteSpace(s.ApiKey))
				{
					s.ApiKey = PlaceholderValue;
					modified = true;
				}
				if (string.IsNullOrWhiteSpace(s.SpeechToTextPrompt))
				{
					s.SpeechToTextPrompt = "Hello, let's use punctuation. Names: Kobus, Piro.";
					// Original code didn't set somethingWasMissing = true here. So, not modified = true.
				}
				if (s.TimeoutSeconds <= 0)
				{
					s.TimeoutSeconds = 10;
					modified = true; // Correcting timeout to default is a modification
				}
			}
		}

		// This check was after the loop in original code
		if (string.IsNullOrWhiteSpace(settings.SpeechToTextHotKey)) // Redundant check, already handled above? No, this is the main one.
		{
			settings.SpeechToTextHotKey = "SHIFT+ALT+U";
			modified = true;
		}
		if (string.IsNullOrWhiteSpace(settings.TempDirectory))
		{
			settings.TempDirectory = @"C:\Temp\Mutation";
			modified = true;
		}

		var duplicateNames = settings.Services
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
		return modified;
	}

	private bool EnsureLlmSettings(LlmSettings settings)
	{
		bool modified = false;
		if (string.IsNullOrWhiteSpace(settings.ApiKey))
		{
			settings.ApiKey = PlaceholderValue;
			modified = true;
		}
		if (string.IsNullOrWhiteSpace(settings.ResourceName))
		{
			settings.ResourceName = "The-Azure-resource-name-for-your-OpenAI-service";
			modified = true;
		}

		if (string.IsNullOrWhiteSpace(settings.FormatTranscriptPrompt))
		{
			settings.FormatTranscriptPrompt = @"You are a helpful proofreader and editor. When you are asked to format a transcript, apply the following rules to improve the formatting of the text:
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
			// Original code didn't set somethingWasMissing = true here.
		}

		if (string.IsNullOrWhiteSpace(settings.ReviewTranscriptPrompt))
		{
			settings.ReviewTranscriptPrompt = @"You are an expert medical AI assistant; your task is to help doctors specialising in radiology.

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
			// Original code didn't set somethingWasMissing = true here.
		}

		if (settings.ModelDeploymentIdMaps == null || !settings.ModelDeploymentIdMaps.Any())
		{
			settings.ModelDeploymentIdMaps = new List<LlmSettings.ModelDeploymentIdMap>
			{
				new LlmSettings.ModelDeploymentIdMap { ModelName = Models.Gpt_3_5_Turbo, DeploymentId = "gpt-35-turbo" },
				new LlmSettings.ModelDeploymentIdMap { ModelName = Models.Gpt_4, DeploymentId = "gpt-4" },
			};
			modified = true; // Adding default list is a modification.
		}

		if (settings.TranscriptFormatRules == null || !settings.TranscriptFormatRules.Any())
		{
			settings.TranscriptFormatRules = new List<LlmSettings.TranscriptFormatRule>
			{
				new LlmSettings.TranscriptFormatRule { Find= "new line", ReplaceWith= $"{Environment.NewLine}", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "newline", ReplaceWith= $"{Environment.NewLine}", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "next line", ReplaceWith= $"{Environment.NewLine}", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "new paragraph", ReplaceWith= $"{Environment.NewLine}{Environment.NewLine}", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "new paragraphs", ReplaceWith= $"{Environment.NewLine}{Environment.NewLine}", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "next paragraph", ReplaceWith= $"{Environment.NewLine}{Environment.NewLine}", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "new bullet", ReplaceWith= $"{Environment.NewLine}- ", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "next bullet", ReplaceWith= $"{Environment.NewLine}- ", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "new colon", ReplaceWith= $": ", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "semicolon", ReplaceWith= $"; ", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "full stop", ReplaceWith= $". ", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "comma", ReplaceWith= $", ", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "exclamation mark", ReplaceWith= $"! ", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "question mark", ReplaceWith= $"? ", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "elipsis", ReplaceWith= $"... ", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
				new LlmSettings.TranscriptFormatRule { Find= "dot dot dot", ReplaceWith= $"... ", CaseSensitive = false, MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Smart },
			};
			modified = true; // Adding default list is a modification.
		}
		return modified;
	}

	private bool EnsureTextToSpeechSettings(TextToSpeechSettings settings)
	{
		bool modified = false;
		if (string.IsNullOrWhiteSpace(settings.TextToSpeechHotKey))
		{
			settings.TextToSpeechHotKey = "CTRL+SHIFT+Q";
			modified = true;
		}
		return modified;
	}

	private bool EnsureHotKeyRouterSettings(HotKeyRouterSettings settings)
	{
		bool modified = false;
		// The original code only checked for null OR not .Any(). 
		// If settings.Mappings is null, it would throw. So, ensure it's not null first.
		if (settings.Mappings == null)
		{
			settings.Mappings = new List<HotKeyRouterSettings.HotKeyRouterMap>();
			modified = true; // Initializing Mappings list is a modification.
		}

		if (!settings.Mappings.Any())
		{
			settings.Mappings.Add(new HotKeyRouterSettings.HotKeyRouterMap("CONTROL+SHIFT+ALT+8", "CONTROL+SHIFT+ALT+9"));
			modified = true; // Adding a default mapping is a modification.
		}
		return modified;
	}

	public void UpgradeSettings()
	{
		string json;
		try
		{
			json = File.ReadAllText(SettingsFileFullPath);
		}
		catch (IOException ex)
		{
			// Log error or handle inability to read file, then exit.
			// For now, we'll rethrow as an indicative problem if truly critical,
			// or simply return if settings upgrade isn't mandatory for app function.
			// Considering this is an upgrade path, if the file is unreadable,
			// it might be corrupted or inaccessible, so not much to do.
			// For now, let's assume if we can't read, we can't upgrade.
			Debug.WriteLine($"Error reading settings file for upgrade: {ex.Message}");
			return;
		}

		JObject jObj;
		try
		{
			jObj = JObject.Parse(json);
		}
		catch (JsonReaderException ex)
		{
			// File is not valid JSON, cannot upgrade.
			Debug.WriteLine($"Error parsing settings file JSON for upgrade: {ex.Message}");
			return;
		}

		bool settingsModified = false;

		// Try to get the SpeetchToTextSettings section
		if (!jObj.TryGetValue("SpeetchToTextSettings", out JToken? sttSettingsToken) || !(sttSettingsToken is JObject sttSettings))
		{
			// If SpeetchToTextSettings doesn't exist or is not an object, nothing to upgrade for this part.
			return;
		}

		// Check if the "Services" array already exists and is an array.
		// If it does, we skip the migration of old flat properties.
		bool servicesArrayExists = sttSettings.TryGetValue("Services", out JToken? servicesToken) && servicesToken.Type == JTokenType.Array;

		if (!servicesArrayExists)
		{
			// Old format: flat properties. Attempt to migrate them.
			// Use TryGetValue for each property and provide defaults if missing.
			string providerName = sttSettings.TryGetValue("Service", out JToken? serviceNameToken) && serviceNameToken.Type == JTokenType.String 
									? serviceNameToken.ToString() 
									: string.Empty;

			JObject serviceObj = new JObject
			{
				["Name"] = providerName, // Use the provider name as the service Name.
				["Provider"] = providerName // Also set the Provider.
			};

			// Migrate other properties safely.
			if (sttSettings.TryGetValue("ApiKey", out JToken? apiKeyToken)) serviceObj["ApiKey"] = apiKeyToken;
			if (sttSettings.TryGetValue("BaseDomain", out JToken? baseDomainToken)) serviceObj["BaseDomain"] = baseDomainToken;
			if (sttSettings.TryGetValue("ModelId", out JToken? modelIdToken)) serviceObj["ModelId"] = modelIdToken;
			if (sttSettings.TryGetValue("SpeechToTextPrompt", out JToken? promptToken)) serviceObj["SpeechToTextPrompt"] = promptToken;
			
			// Create a new services array with the service object as the first element.
			JArray servicesArray = new JArray { serviceObj };

			// Remove the old flat properties that have been migrated.
			sttSettings.Remove("Service");
			sttSettings.Remove("ApiKey");
			sttSettings.Remove("BaseDomain");
			sttSettings.Remove("ModelId");
			sttSettings.Remove("SpeechToTextPrompt");

			// Add the new properties: the active service and the services array.
			sttSettings["ActiveSpeetchToTextService"] = providerName; // Set based on old "Service" name.
			sttSettings["Services"] = servicesArray;
			settingsModified = true;
		}

		// Second part of upgrade: Rename "OpenAiWhisper" provider to "OpenAi"
		// This should run regardless of whether the first migration happened,
		// as the "Services" array might exist but still contain the old provider name.
		if (sttSettings.TryGetValue("Services", out JToken? currentServicesToken) && currentServicesToken is JArray servicesArrayForProviderUpdate)
		{
			foreach (JToken serviceEntry in servicesArrayForProviderUpdate)
			{
				if (serviceEntry is JObject serviceObject) // Ensure each entry is an object
				{
					if (serviceObject.TryGetValue("Provider", out JToken? providerToken) && 
						providerToken.Type == JTokenType.String && 
						providerToken.ToString() == "OpenAiWhisper")
					{
						serviceObject["Provider"] = "OpenAi";
						settingsModified = true;
					}
				}
			}
		}

		if (settingsModified)
		{
			try
			{
				File.WriteAllText(SettingsFileFullPath, jObj.ToString(Formatting.Indented), Encoding.UTF8);
			}
			catch (IOException ex)
			{
				// Log error or handle inability to write file.
				Debug.WriteLine($"Error writing updated settings file after upgrade: {ex.Message}");
			}
		}
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
