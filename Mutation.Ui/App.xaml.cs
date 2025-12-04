using CognitiveSupport;
using CoreAudio;
using Deepgram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Mutation.Ui.Services;
using OpenAI;
using OpenAI.Audio;
using Azure.AI.OpenAI;
using System.ClientModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace Mutation.Ui;

public partial class App : Application
{
        private Window? _window;
	private IHost? _host;
	private const string OpenAiHttpClientName = "openai-http-client";
	private bool _isShuttingDown = false;

        public App()
        {

        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
                try
		{
			HostApplicationBuilder builder = Host.CreateApplicationBuilder();

			string exeDir = AppDomain.CurrentDomain.BaseDirectory;
			string mutationDir = exeDir;
			string filePath = Path.Combine(mutationDir, "Mutation.json");
			var settingsManager = new SettingsManager(filePath);
			var settings = settingsManager.LoadAndEnsureSettings();
			BeepPlayer.Initialize(settings);

			builder.Services.AddSingleton<ISettingsManager>(settingsManager);
			builder.Services.AddSingleton(settings);
			builder.Services.AddSingleton<ClipboardManager>();
			builder.Services.AddSingleton<UiStateManager>();
			builder.Services.AddSingleton<MMDeviceEnumerator>(_ => new MMDeviceEnumerator(Guid.NewGuid()));
			builder.Services.AddSingleton<AudioDeviceManager>();
			builder.Services.AddSingleton<IOcrService>(sp =>
	 new OcrService(
		  settings.AzureComputerVisionSettings?.ApiKey,
		  settings.AzureComputerVisionSettings?.Endpoint,
		  settings.AzureComputerVisionSettings?.TimeoutSeconds ?? 10));
			builder.Services.AddSingleton<OcrManager>(sp =>
					  new OcrManager(settings,
									  sp.GetRequiredService<IOcrService>(),
									  sp.GetRequiredService<ClipboardManager>()));
			builder.Services.AddSingleton<HotkeyManager>(sp =>
					  new HotkeyManager(sp.GetRequiredService<MainWindow>(), sp.GetRequiredService<Settings>()));
			builder.Services.AddSingleton<ILlmService>(
					  new LlmService(
							 settings.LlmSettings?.ApiKey ?? string.Empty,
							 settings.LlmSettings?.Models ?? new List<string>(),
							 settings.LlmSettings?.ReasoningEffort ?? "low"));
			builder.Services.AddSingleton<TranscriptFormatter>();
                        builder.Services.AddSingleton<ITextToSpeechService, TextToSpeechService>();
			builder.Services.AddHttpClient(OpenAiHttpClientName);
			AddSpeechToTextServices(builder, settings);
			builder.Services.AddSingleton<MainWindow>();

			_host = builder.Build();

			_window = _host.Services.GetRequiredService<MainWindow>();
			var ui = _host.Services.GetRequiredService<UiStateManager>();
			ui.Restore(_window);

			_window.Activate();

                        var preflight = ScreenCapturePreflight.TryCaptureProbe();
			if (!preflight.ok)
			{
				string title = "Screen Capture Disabled";
				string message = preflight.message ?? "Screen capture may be disabled by system policy.";
				if (_window.Content is FrameworkElement fe0 && fe0.XamlRoot is not null)
				{
					var dialog = new ContentDialog
					{
						Title = title,
						Content = new TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
						CloseButtonText = "OK",
						XamlRoot = fe0.XamlRoot
					};
					Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dialog, title);
					Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(dialog, message);
					await dialog.ShowAsync();
				}
				else
				{
					System.Windows.Forms.MessageBox.Show(message, title,
						System.Windows.Forms.MessageBoxButtons.OK,
						System.Windows.Forms.MessageBoxIcon.Warning);
				}
			}

			if (BeepPlayer.LastInitializationIssues.Count > 0)
			{
				const string title = "Custom Beep Settings Issues";
				string message = "The following issues were found with the custom beep settings:\n\n" +
										  string.Join("\n", BeepPlayer.LastInitializationIssues);

				if (_window.Content is FrameworkElement fe && fe.XamlRoot is not null)
				{
					var dialog = new ContentDialog
					{
						Title = title,
						Content = new TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
						CloseButtonText = "OK",
						XamlRoot = fe.XamlRoot
					};
					// Provide accessible name/help text for screen readers
					Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dialog, title);
					Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(dialog, message);
					await dialog.ShowAsync();
				}
				else
				{
					System.Windows.Forms.MessageBox.Show(
							  message,
							  title,
							  System.Windows.Forms.MessageBoxButtons.OK,
							  System.Windows.Forms.MessageBoxIcon.Warning);
				}
			}

			var ocrMgr = _host.Services.GetRequiredService<OcrManager>();
			ocrMgr.InitializeWindow(_window);

                        var hkManager = _host.Services.GetRequiredService<HotkeyManager>();
                        if (_window is MainWindow main)
                                main.AttachHotkeyManager(hkManager);
                        var settingsSvc = _host.Services.GetRequiredService<Settings>();

                        if (!string.IsNullOrWhiteSpace(settingsSvc.AzureComputerVisionSettings?.ScreenshotHotKey))
                        {
                                hkManager.RegisterHotkey(
						  Hotkey.Parse(settingsSvc.AzureComputerVisionSettings.ScreenshotHotKey!),
						  async () =>
						  {
							  try
							  {
								  await ocrMgr.TakeScreenshotToClipboardAsync();
							  }
							  catch (Exception ex) { await ((MainWindow)_window).ShowErrorDialog("Screenshot Error", ex); }
						  });
			}

			if (!string.IsNullOrWhiteSpace(settingsSvc.AzureComputerVisionSettings?.ScreenshotOcrHotKey))
			{
				hkManager.RegisterHotkey(
						  Hotkey.Parse(settingsSvc.AzureComputerVisionSettings.ScreenshotOcrHotKey!),
						  async () =>
						  {
							  try
							  {
								  var result = await ocrMgr.TakeScreenshotAndExtractTextAsync(OcrReadingOrder.TopToBottomColumnAware);
								  var mainWindow = _host.Services.GetRequiredService<MainWindow>();
								  mainWindow.SetOcrText(result.Message);
                                                              HotkeyManager.SendHotkeyAfterDelay(settingsSvc.AzureComputerVisionSettings?.SendHotkeyAfterOcrOperation, result.Success ? Constants.SendHotkeyDelay : Constants.FailureSendHotkeyDelay);
							  }
							  catch (Exception ex) { await ((MainWindow)_window).ShowErrorDialog("Screenshot + OCR Error", ex); }
						  });
			}

			if (!string.IsNullOrWhiteSpace(settingsSvc.AzureComputerVisionSettings?.ScreenshotLeftToRightTopToBottomOcrHotKey))
			{
				hkManager.RegisterHotkey(
						  Hotkey.Parse(settingsSvc.AzureComputerVisionSettings.ScreenshotLeftToRightTopToBottomOcrHotKey!),
						  async () =>
						  {
							  try
							  {
								  var result = await ocrMgr.TakeScreenshotAndExtractTextAsync(OcrReadingOrder.LeftToRightTopToBottom);
								  var mainWindow = _host.Services.GetRequiredService<MainWindow>();
								  mainWindow.SetOcrText(result.Message);
                                                              HotkeyManager.SendHotkeyAfterDelay(settingsSvc.AzureComputerVisionSettings?.SendHotkeyAfterOcrOperation, result.Success ? Constants.SendHotkeyDelay : Constants.FailureSendHotkeyDelay);
							  }
							  catch (Exception ex) { await ((MainWindow)_window).ShowErrorDialog("Screenshot + OCR (LRTB) Error", ex); }
						  });
			}

			if (!string.IsNullOrWhiteSpace(settingsSvc.AzureComputerVisionSettings?.OcrHotKey))
			{
				hkManager.RegisterHotkey(
						  Hotkey.Parse(settingsSvc.AzureComputerVisionSettings.OcrHotKey!),
						  async () =>
						  {
							  try
							  {
								  var result = await ocrMgr.ExtractTextFromClipboardImageAsync(OcrReadingOrder.TopToBottomColumnAware);
								  var mainWindow = _host.Services.GetRequiredService<MainWindow>();
								  mainWindow.SetOcrText(result.Message);
                                                              HotkeyManager.SendHotkeyAfterDelay(settingsSvc.AzureComputerVisionSettings?.SendHotkeyAfterOcrOperation, result.Success ? Constants.SendHotkeyDelay : Constants.FailureSendHotkeyDelay);
							  }
							  catch (Exception ex) { await ((MainWindow)_window).ShowErrorDialog("OCR Clipboard Error", ex); }
						  });
			}

			if (!string.IsNullOrWhiteSpace(settingsSvc.AzureComputerVisionSettings?.OcrLeftToRightTopToBottomHotKey))
			{
				hkManager.RegisterHotkey(
						  Hotkey.Parse(settingsSvc.AzureComputerVisionSettings.OcrLeftToRightTopToBottomHotKey!),
						  async () =>
						  {
							  try
							  {
								  var result = await ocrMgr.ExtractTextFromClipboardImageAsync(OcrReadingOrder.LeftToRightTopToBottom);
								  var mainWindow = _host.Services.GetRequiredService<MainWindow>();
								  mainWindow.SetOcrText(result.Message);
                                                              HotkeyManager.SendHotkeyAfterDelay(settingsSvc.AzureComputerVisionSettings?.SendHotkeyAfterOcrOperation, result.Success ? Constants.SendHotkeyDelay : Constants.FailureSendHotkeyDelay);
							  }
							  catch (Exception ex) { await ((MainWindow)_window).ShowErrorDialog("OCR Clipboard (LRTB) Error", ex); }
						  });
			}

			if (!string.IsNullOrWhiteSpace(settingsSvc.AudioSettings?.MicrophoneToggleMuteHotKey))
			{
				hkManager.RegisterHotkey(
						  Hotkey.Parse(settingsSvc.AudioSettings.MicrophoneToggleMuteHotKey!),
						  () =>
						  {
							  try { _window.DispatcherQueue.TryEnqueue(() => ((MainWindow)_window).BtnToggleMic_Click(null!, null!)); }
							  catch (Exception ex) { _window.DispatcherQueue.TryEnqueue(async () => await ((MainWindow)_window).ShowErrorDialog("Toggle Mic Error", ex)); }
						  });
			}

			if (!string.IsNullOrWhiteSpace(settingsSvc.SpeechToTextSettings?.SpeechToTextHotKey))
			{
				hkManager.RegisterHotkey(
						  Hotkey.Parse(settingsSvc.SpeechToTextSettings.SpeechToTextHotKey!),
						  () =>
						  {
							  try { _window.DispatcherQueue.TryEnqueue(async () => await ((MainWindow)_window).StartStopSpeechToTextAsync()); }
							  catch (Exception ex) { _window.DispatcherQueue.TryEnqueue(async () => await ((MainWindow)_window).ShowErrorDialog("Speech to Text Error", ex)); }
						  });
			}

			if (!string.IsNullOrWhiteSpace(settingsSvc.TextToSpeechSettings?.TextToSpeechHotKey))
			{
				hkManager.RegisterHotkey(
						  Hotkey.Parse(settingsSvc.TextToSpeechSettings.TextToSpeechHotKey!),
						  () =>
						  {
							  try { _window.DispatcherQueue.TryEnqueue(() => ((MainWindow)_window).BtnTextToSpeech_Click(null!, null!)); }
							  catch (Exception ex) { _window.DispatcherQueue.TryEnqueue(async () => await ((MainWindow)_window).ShowErrorDialog("Text to Speech Error", ex)); }
						  });
			}

                        _ = hkManager.RegisterRouterHotkeys();

			if (hkManager.FailedRegistrations.Count > 0)
			{
				const string title = "Hotkeys Not Registered";
				string message = "The following hotkeys could not be registered and may be in use by another application:\n\n" +
										  string.Join("\n", hkManager.FailedRegistrations);

				if (_window.Content is FrameworkElement fe && fe.XamlRoot is not null)
				{
					var dialog = new ContentDialog
					{
						Title = title,
						Content = new TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
						CloseButtonText = "OK",
						XamlRoot = fe.XamlRoot
					};
					// Provide accessible name/help text for screen readers
					Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dialog, title);
					Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(dialog, message);
					await dialog.ShowAsync();
				}
				else
				{
					System.Windows.Forms.MessageBox.Show(
							  message,
							  title,
							  System.Windows.Forms.MessageBoxButtons.OK,
							  System.Windows.Forms.MessageBoxIcon.Warning);
				}
			}

			_window.Closed += async (_, __) =>
			{
				// Ensure global hooks are released promptly
				try { hkManager.Dispose(); } catch { }
				// Stop background host services and exit the app
				await ShutdownAsync();
			};
		}
		catch (Exception ex)
		{
			bool dialogShown = false;
			try
			{
				var errorDialog = new ContentDialog
				{
					Title = "Startup Error",
					Content = new TextBlock { Text = $"An error occurred during startup:\n\n{ex}", TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
					CloseButtonText = "OK"
				};
				// Accessibility: set name/help text so screen readers announce the dialog clearly
				Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(errorDialog, "Startup Error");
				Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(errorDialog, ex.ToString());
				// Try to set XamlRoot if available
				if (_window is not null && _window.Content is FrameworkElement fe && fe.XamlRoot is not null)
					errorDialog.XamlRoot = fe.XamlRoot;
				else if (Microsoft.UI.Xaml.Window.Current?.Content is FrameworkElement fe2 && fe2.XamlRoot is not null)
					errorDialog.XamlRoot = fe2.XamlRoot;

				if (errorDialog.XamlRoot != null)
				{
					await errorDialog.ShowAsync();
					dialogShown = true;
				}
			}
			catch
			{
				// Ignore dialog errors, fallback below
			}
			if (!dialogShown)
			{
				// Fallback: use a WinForms message box (requires reference to System.Windows.Forms)
				System.Windows.Forms.MessageBox.Show(
					$"An error occurred during startup:\n\n{ex}",
					"Startup Error",
					System.Windows.Forms.MessageBoxButtons.OK,
					System.Windows.Forms.MessageBoxIcon.Error
				);
			}
		}
	}

	private async System.Threading.Tasks.Task ShutdownAsync()
	{
		if (_isShuttingDown)
			return;
		_isShuttingDown = true;
		try
		{
			if (_host is not null)
			{
				try
				{
					await _host.StopAsync(TimeSpan.FromSeconds(2));
				}
				catch { }
				try
				{
					_host.Dispose();
				}
				catch { }
				_host = null;
			}
		}
		finally
		{
			// Request application shutdown; if background threads keep process alive,
			// Exit() will terminate the message loop.
			try { Exit(); } catch { }
		}
	}

	private static void AddSpeechToTextServices(HostApplicationBuilder builder, Settings settings)
	{
		builder.Services.AddSingleton<ISpeechToTextService[]>(sp =>
		{
			List<ISpeechToTextService> services = new();
			var sttSettings = settings.SpeechToTextSettings?.Services ?? Array.Empty<SpeechToTextServiceSettings>();
			foreach (var serviceSettings in sttSettings)
			{
				switch (serviceSettings.Provider)
				{
					case SpeechToTextProviders.OpenAi:
						services.Add(CreateWhisperSpeechToTextService(builder, serviceSettings, sp));
						break;
					case SpeechToTextProviders.Deepgram:
						services.Add(CreateDeepgramSpeechToTextService(builder, serviceSettings));
						break;
					default:
						throw new NotSupportedException($"The SpeechToText service '{serviceSettings.Provider}' is not supported.");
				}
			}
			return services.ToArray();
		});
	}

	private static ISpeechToTextService CreateWhisperSpeechToTextService(HostApplicationBuilder builder, SpeechToTextServiceSettings serviceSettings, IServiceProvider sp)
	{
		string baseDomain = serviceSettings.BaseDomain?.Trim() ?? string.Empty;
		string apiKey = serviceSettings.ApiKey ?? string.Empty;
		string modelId = serviceSettings.ModelId ?? string.Empty;

		AudioClient audioClient;
		if (!string.IsNullOrEmpty(baseDomain))
		{
			if (!baseDomain.EndsWith("/v1") && !baseDomain.EndsWith("/v1/"))
			{
				baseDomain = baseDomain.TrimEnd('/') + "/v1/";
			}
			var options = new OpenAIClientOptions { Endpoint = new Uri(baseDomain) };
			var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
			audioClient = client.GetAudioClient(modelId);
		}
		else
		{
			audioClient = new AudioClient(modelId, new ApiKeyCredential(apiKey));
		}

		return new OpenAiSpeechToTextService(
				  serviceSettings.Name ?? string.Empty,
				  audioClient,
				  serviceSettings.TimeoutSeconds > 0 ? serviceSettings.TimeoutSeconds : 10);
	}

	private static ISpeechToTextService CreateDeepgramSpeechToTextService(HostApplicationBuilder builder, SpeechToTextServiceSettings serviceSettings)
	{
		Deepgram.Clients.Interfaces.v1.IListenRESTClient deepgramClient = ClientFactory.CreateListenRESTClient(serviceSettings.ApiKey ?? string.Empty);

		return new DeepgramSpeechToTextService(
				  serviceSettings.Name ?? string.Empty,
				  deepgramClient,
				  serviceSettings.ModelId ?? string.Empty,
				  serviceSettings.TimeoutSeconds > 0 ? serviceSettings.TimeoutSeconds : 10);
	}
}
