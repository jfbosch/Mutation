using CognitiveSupport;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mutation.Ui;

internal static class SettingsExtensions
{
        public static Settings DeepClone(this Settings source)
        {
                if (source is null)
                        throw new ArgumentNullException(nameof(source));

                var clone = new Settings
                {
                        UserInstructions = source.UserInstructions,
                        AudioSettings = CloneAudioSettings(source.AudioSettings),
                        AzureComputerVisionSettings = CloneOcrSettings(source.AzureComputerVisionSettings),
                        SpeechToTextSettings = CloneSpeechToTextSettings(source.SpeechToTextSettings),
                        LlmSettings = CloneLlmSettings(source.LlmSettings),
                        TextToSpeechSettings = CloneTextToSpeechSettings(source.TextToSpeechSettings),
                        MainWindowUiSettings = CloneMainWindowSettings(source.MainWindowUiSettings),
                        HotKeyRouterSettings = CloneRouterSettings(source.HotKeyRouterSettings)
                };

                return clone;
        }

        public static void CopyFrom(this Settings target, Settings source)
        {
                if (target is null)
                        throw new ArgumentNullException(nameof(target));
                if (source is null)
                        throw new ArgumentNullException(nameof(source));

                target.UserInstructions = source.UserInstructions;
                target.AudioSettings = CloneAudioSettings(source.AudioSettings);
                target.AzureComputerVisionSettings = CloneOcrSettings(source.AzureComputerVisionSettings);
                target.SpeechToTextSettings = CloneSpeechToTextSettings(source.SpeechToTextSettings);
                target.LlmSettings = CloneLlmSettings(source.LlmSettings);
                target.TextToSpeechSettings = CloneTextToSpeechSettings(source.TextToSpeechSettings);
                target.MainWindowUiSettings = CloneMainWindowSettings(source.MainWindowUiSettings);
                target.HotKeyRouterSettings = CloneRouterSettings(source.HotKeyRouterSettings);
        }

        private static AudioSettings? CloneAudioSettings(AudioSettings? source)
        {
                if (source is null)
                        return null;

                return new AudioSettings
                {
                        ActiveCaptureDeviceFullName = source.ActiveCaptureDeviceFullName,
                        MicrophoneToggleMuteHotKey = source.MicrophoneToggleMuteHotKey,
                        EnableMicrophoneVisualization = source.EnableMicrophoneVisualization,
                        CustomBeepSettings = CloneCustomBeepSettings(source.CustomBeepSettings)
                };
        }

        private static AudioSettings.CustomBeepSettingsData? CloneCustomBeepSettings(AudioSettings.CustomBeepSettingsData? source)
        {
                if (source is null)
                        return null;

                return new AudioSettings.CustomBeepSettingsData
                {
                        UseCustomBeeps = source.UseCustomBeeps,
                        BeepSuccessFile = source.BeepSuccessFile,
                        BeepFailureFile = source.BeepFailureFile,
                        BeepStartFile = source.BeepStartFile,
                        BeepEndFile = source.BeepEndFile,
                        BeepMuteFile = source.BeepMuteFile,
                        BeepUnmuteFile = source.BeepUnmuteFile
                };
        }

        private static AzureComputerVisionSettings? CloneOcrSettings(AzureComputerVisionSettings? source)
        {
                if (source is null)
                        return null;

                return new AzureComputerVisionSettings
                {
                        InvertScreenshot = source.InvertScreenshot,
                        ScreenshotHotKey = source.ScreenshotHotKey,
                        ScreenshotOcrHotKey = source.ScreenshotOcrHotKey,
                        ScreenshotLeftToRightTopToBottomOcrHotKey = source.ScreenshotLeftToRightTopToBottomOcrHotKey,
                        OcrHotKey = source.OcrHotKey,
                        OcrLeftToRightTopToBottomHotKey = source.OcrLeftToRightTopToBottomHotKey,
                        SendHotkeyAfterOcrOperation = source.SendHotkeyAfterOcrOperation,
                        ApiKey = source.ApiKey,
                        Endpoint = source.Endpoint,
                        TimeoutSeconds = source.TimeoutSeconds
                };
        }

        private static SpeechToTextSettings? CloneSpeechToTextSettings(SpeechToTextSettings? source)
        {
                if (source is null)
                        return null;

                return new SpeechToTextSettings
                {
                        TempDirectory = source.TempDirectory,
                        SpeechToTextHotKey = source.SpeechToTextHotKey,
                        SendHotkeyAfterTranscriptionOperation = source.SendHotkeyAfterTranscriptionOperation,
                        Services = source.Services?.Select(CloneSpeechService).ToArray(),
                        ActiveSpeechToTextService = source.ActiveSpeechToTextService
                };
        }

        private static SpeechToTextServiceSettings CloneSpeechService(SpeechToTextServiceSettings source)
        {
                return new SpeechToTextServiceSettings
                {
                        Name = source.Name,
                        Provider = source.Provider,
                        ApiKey = source.ApiKey,
                        BaseDomain = source.BaseDomain,
                        ModelId = source.ModelId,
                        SpeechToTextPrompt = source.SpeechToTextPrompt,
                        TimeoutSeconds = source.TimeoutSeconds
                };
        }

        private static LlmSettings? CloneLlmSettings(LlmSettings? source)
        {
                if (source is null)
                        return null;

                return new LlmSettings
                {
                        ApiKey = source.ApiKey,
                        ResourceName = source.ResourceName,
                        FormatTranscriptPrompt = source.FormatTranscriptPrompt,
                        ModelDeploymentIdMaps = source.ModelDeploymentIdMaps?.Select(CloneDeployment).ToList() ?? new(),
                        TranscriptFormatRules = source.TranscriptFormatRules?.Select(CloneRule).ToList() ?? new()
                };
        }

        private static LlmSettings.ModelDeploymentIdMap CloneDeployment(LlmSettings.ModelDeploymentIdMap source)
        {
                return new LlmSettings.ModelDeploymentIdMap
                {
                        ModelName = source.ModelName,
                        DeploymentId = source.DeploymentId
                };
        }

        private static LlmSettings.TranscriptFormatRule CloneRule(LlmSettings.TranscriptFormatRule source)
        {
                return new LlmSettings.TranscriptFormatRule
                {
                        Find = source.Find,
                        ReplaceWith = source.ReplaceWith,
                        CaseSensitive = source.CaseSensitive,
                        MatchType = source.MatchType
                };
        }

        private static TextToSpeechSettings? CloneTextToSpeechSettings(TextToSpeechSettings? source)
        {
                if (source is null)
                        return null;

                return new TextToSpeechSettings
                {
                        TextToSpeechHotKey = source.TextToSpeechHotKey
                };
        }

        private static MainWindowUiSettings CloneMainWindowSettings(MainWindowUiSettings? source)
        {
                if (source is null)
                        return new MainWindowUiSettings();

                return new MainWindowUiSettings
                {
                        WindowLocation = source.WindowLocation,
                        WindowSize = source.WindowSize,
                        MaxTextBoxLineCount = source.MaxTextBoxLineCount
                };
        }

        private static HotKeyRouterSettings CloneRouterSettings(HotKeyRouterSettings? source)
        {
                if (source is null)
                        return new HotKeyRouterSettings();

                return new HotKeyRouterSettings
                {
                        Mappings = source.Mappings?.Select(CloneRouterMap).ToList() ?? new List<HotKeyRouterSettings.HotKeyRouterMap>()
                };
        }

        private static HotKeyRouterSettings.HotKeyRouterMap CloneRouterMap(HotKeyRouterSettings.HotKeyRouterMap source)
        {
                return new HotKeyRouterSettings.HotKeyRouterMap(source.FromHotKey ?? string.Empty, source.ToHotKey ?? string.Empty);
        }
}
