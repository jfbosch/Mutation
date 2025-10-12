using CognitiveSupport;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using SettingsModel = CognitiveSupport.Settings;

namespace Mutation.Ui.ViewModels.Settings;

internal enum SettingsSection
{
        General,
        Audio,
        SpeechToText,
        Llm,
        Ocr,
        TextToSpeech,
        Interface,
        HotkeyRouter
}

internal sealed class SettingsSectionItem
{
        public SettingsSectionItem(SettingsSection section, string title, string description)
        {
                Section = section;
                Title = title;
                Description = description;
        }

        public SettingsSection Section { get; }
        public string Title { get; }
        public string Description { get; }
}

internal sealed class SettingsDialogViewModel : ObservableObject
{
        private readonly SettingsModel _original;
        private SettingsSectionItem? _selectedSection;
        private readonly RelayCommand _addSpeechServiceCommand;
        private readonly RelayCommand<SpeechServiceEntryViewModel> _removeSpeechServiceCommand;
        private readonly RelayCommand _addDeploymentCommand;
        private readonly RelayCommand<ModelDeploymentMapViewModel> _removeDeploymentCommand;
        private readonly RelayCommand _addRuleCommand;
        private readonly RelayCommand<TranscriptFormatRuleViewModel> _removeRuleCommand;
        private readonly RelayCommand _addHotkeyRouteCommand;
        private readonly RelayCommand<HotkeyRouteViewModel> _removeHotkeyRouteCommand;
        private bool _isDirty;
        private readonly ObservableCollection<string> _validationErrors = new();

        public SettingsDialogViewModel(SettingsModel settings)
        {
                _original = settings ?? throw new ArgumentNullException(nameof(settings));
                WorkingCopy = settings.DeepClone();
                WorkingCopy.AudioSettings ??= new AudioSettings();
                WorkingCopy.AudioSettings.CustomBeepSettings ??= new AudioSettings.CustomBeepSettingsData();
                WorkingCopy.SpeechToTextSettings ??= new SpeechToTextSettings();
                if (string.IsNullOrWhiteSpace(WorkingCopy.SpeechToTextSettings.TempDirectory))
                        WorkingCopy.SpeechToTextSettings.TempDirectory = settings.SpeechToTextSettings?.TempDirectory ?? string.Empty;
                WorkingCopy.LlmSettings ??= new LlmSettings();
                WorkingCopy.HotKeyRouterSettings ??= new HotKeyRouterSettings();
                WorkingCopy.AzureComputerVisionSettings ??= new AzureComputerVisionSettings();
                WorkingCopy.TextToSpeechSettings ??= new TextToSpeechSettings();
                WorkingCopy.MainWindowUiSettings ??= new MainWindowUiSettings();

                Sections = new ObservableCollection<SettingsSectionItem>
                {
                        new(SettingsSection.General, "General", "Workspace-level preferences"),
                        new(SettingsSection.Audio, "Audio", "Microphone and feedback"),
                        new(SettingsSection.SpeechToText, "Speech to text", "Recording and provider settings"),
                        new(SettingsSection.Llm, "LLM", "Transcript formatting rules"),
                        new(SettingsSection.Ocr, "OCR", "Screenshot and recognition"),
                        new(SettingsSection.TextToSpeech, "Text to speech", "Playback shortcuts"),
                        new(SettingsSection.Interface, "Interface", "Layout and behavior"),
                        new(SettingsSection.HotkeyRouter, "Hotkey router", "Redirect shortcuts to new targets"),
                };

                SelectedSectionItem = Sections.FirstOrDefault();

                SpeechServices = new ObservableCollection<SpeechServiceEntryViewModel>((WorkingCopy.SpeechToTextSettings?.Services ?? Array.Empty<SpeechToTextServiceSettings>()).Select(s => new SpeechServiceEntryViewModel(this, s)));
                ModelDeploymentMaps = new ObservableCollection<ModelDeploymentMapViewModel>((WorkingCopy.LlmSettings?.ModelDeploymentIdMaps ?? new List<LlmSettings.ModelDeploymentIdMap>()).Select(m => new ModelDeploymentMapViewModel(this, m)));
                TranscriptRules = new ObservableCollection<TranscriptFormatRuleViewModel>((WorkingCopy.LlmSettings?.TranscriptFormatRules ?? new List<LlmSettings.TranscriptFormatRule>()).Select(r => new TranscriptFormatRuleViewModel(this, r)));
                HotkeyRoutes = new ObservableCollection<HotkeyRouteViewModel>((WorkingCopy.HotKeyRouterSettings?.Mappings ?? new List<HotKeyRouterSettings.HotKeyRouterMap>()).Select(m => new HotkeyRouteViewModel(this, m)));

                foreach (var route in HotkeyRoutes)
                        AttachChangeTracking(route);
                foreach (var svc in SpeechServices)
                        AttachChangeTracking(svc);
                foreach (var rule in TranscriptRules)
                        AttachChangeTracking(rule);
                foreach (var map in ModelDeploymentMaps)
                        AttachChangeTracking(map);

                SpeechServices.CollectionChanged += (_, __) => { MarkDirty(); RecalculateValidation(); };
                ModelDeploymentMaps.CollectionChanged += (_, __) => { MarkDirty(); RecalculateValidation(); };
                TranscriptRules.CollectionChanged += (_, __) => { MarkDirty(); RecalculateValidation(); };
                HotkeyRoutes.CollectionChanged += (_, __) => { MarkDirty(); RecalculateValidation(); };

                _addSpeechServiceCommand = new RelayCommand(() =>
                {
                        var entry = new SpeechServiceEntryViewModel(this, new SpeechToTextServiceSettings { Provider = SpeechToTextProviders.OpenAi, TimeoutSeconds = 10 });
                        AttachChangeTracking(entry);
                        SpeechServices.Add(entry);
                });
                _removeSpeechServiceCommand = new RelayCommand<SpeechServiceEntryViewModel>(entry =>
                {
                        if (entry is null)
                                return;
                        SpeechServices.Remove(entry);
                });

                _addDeploymentCommand = new RelayCommand(() =>
                {
                        var vm = new ModelDeploymentMapViewModel(this, new LlmSettings.ModelDeploymentIdMap());
                        AttachChangeTracking(vm);
                        ModelDeploymentMaps.Add(vm);
                });
                _removeDeploymentCommand = new RelayCommand<ModelDeploymentMapViewModel>(vm =>
                {
                        if (vm is null)
                                return;
                        ModelDeploymentMaps.Remove(vm);
                });

                _addRuleCommand = new RelayCommand(() =>
                {
                        var rule = new TranscriptFormatRuleViewModel(this, new LlmSettings.TranscriptFormatRule { MatchType = LlmSettings.TranscriptFormatRule.MatchTypeEnum.Plain });
                        AttachChangeTracking(rule);
                        TranscriptRules.Add(rule);
                });
                _removeRuleCommand = new RelayCommand<TranscriptFormatRuleViewModel>(vm =>
                {
                        if (vm is null)
                                return;
                        TranscriptRules.Remove(vm);
                });

                _addHotkeyRouteCommand = new RelayCommand(() =>
                {
                        var route = new HotkeyRouteViewModel(this, new HotKeyRouterSettings.HotKeyRouterMap(string.Empty, string.Empty));
                        AttachChangeTracking(route);
                        HotkeyRoutes.Add(route);
                });
                _removeHotkeyRouteCommand = new RelayCommand<HotkeyRouteViewModel>(route =>
                {
                        if (route is null)
                                return;
                        HotkeyRoutes.Remove(route);
                });

                RecalculateValidation();
        }

        public SettingsModel WorkingCopy { get; }

        public ObservableCollection<SettingsSectionItem> Sections { get; }

        public SettingsSectionItem? SelectedSectionItem
        {
                get => _selectedSection;
                set
                {
                        if (SetProperty(ref _selectedSection, value))
                                OnSelectedSectionChanged();
                }
        }

        public SettingsSection SelectedSection => SelectedSectionItem?.Section ?? SettingsSection.General;

        public bool HasValidationErrors => _validationErrors.Count > 0;

        public Visibility GeneralSectionVisibility => GetSectionVisibility(SettingsSection.General);
        public Visibility AudioSectionVisibility => GetSectionVisibility(SettingsSection.Audio);
        public Visibility SpeechToTextSectionVisibility => GetSectionVisibility(SettingsSection.SpeechToText);
        public Visibility LlmSectionVisibility => GetSectionVisibility(SettingsSection.Llm);
        public Visibility OcrSectionVisibility => GetSectionVisibility(SettingsSection.Ocr);
        public Visibility TextToSpeechSectionVisibility => GetSectionVisibility(SettingsSection.TextToSpeech);
        public Visibility InterfaceSectionVisibility => GetSectionVisibility(SettingsSection.Interface);
        public Visibility HotkeyRouterSectionVisibility => GetSectionVisibility(SettingsSection.HotkeyRouter);

        public bool IsGeneralSectionVisible => IsSectionVisible(SettingsSection.General);
        public bool IsAudioSectionVisible => IsSectionVisible(SettingsSection.Audio);
        public bool IsSpeechToTextSectionVisible => IsSectionVisible(SettingsSection.SpeechToText);
        public bool IsLlmSectionVisible => IsSectionVisible(SettingsSection.Llm);
        public bool IsOcrSectionVisible => IsSectionVisible(SettingsSection.Ocr);
        public bool IsTextToSpeechSectionVisible => IsSectionVisible(SettingsSection.TextToSpeech);
        public bool IsInterfaceSectionVisible => IsSectionVisible(SettingsSection.Interface);
        public bool IsHotkeyRouterSectionVisible => IsSectionVisible(SettingsSection.HotkeyRouter);

        public ObservableCollection<SpeechServiceEntryViewModel> SpeechServices { get; }
        public ObservableCollection<ModelDeploymentMapViewModel> ModelDeploymentMaps { get; }
        public ObservableCollection<TranscriptFormatRuleViewModel> TranscriptRules { get; }
        public ObservableCollection<HotkeyRouteViewModel> HotkeyRoutes { get; }

        public IReadOnlyList<SpeechToTextProviders> ProviderOptions { get; } = Enum.GetValues(typeof(SpeechToTextProviders)).Cast<SpeechToTextProviders>().ToArray();
        public IReadOnlyList<LlmSettings.TranscriptFormatRule.MatchTypeEnum> MatchTypeOptions { get; } = Enum.GetValues(typeof(LlmSettings.TranscriptFormatRule.MatchTypeEnum)).Cast<LlmSettings.TranscriptFormatRule.MatchTypeEnum>().ToArray();

        public double OcrTimeoutSeconds
        {
                get => WorkingCopy.AzureComputerVisionSettings.TimeoutSeconds;
                set
                {
                        int coerced = (int)Math.Max(1, Math.Round(value));
                        if (WorkingCopy.AzureComputerVisionSettings.TimeoutSeconds != coerced)
                        {
                                WorkingCopy.AzureComputerVisionSettings.TimeoutSeconds = coerced;
                                MarkDirty();
                                RecalculateValidation();
                                OnPropertyChanged();
                        }
                }
        }

        public double MaxTextAreaLines
        {
                get => WorkingCopy.MainWindowUiSettings.MaxTextBoxLineCount;
                set
                {
                        int coerced = (int)Math.Max(1, Math.Round(value));
                        if (WorkingCopy.MainWindowUiSettings.MaxTextBoxLineCount != coerced)
                        {
                                WorkingCopy.MainWindowUiSettings.MaxTextBoxLineCount = coerced;
                                MarkDirty();
                                OnPropertyChanged();
                        }
                }
        }

        public IReadOnlyList<string> ValidationErrors => _validationErrors;

        public bool CanSave => !_validationErrors.Any();

        public bool IsDirty
        {
                get => _isDirty;
                private set
                {
                        if (SetProperty(ref _isDirty, value))
                        {
                                _addSpeechServiceCommand.RaiseCanExecuteChanged();
                        }
                }
        }

        public ICommand AddSpeechServiceCommand => _addSpeechServiceCommand;
        public ICommand RemoveSpeechServiceCommand => _removeSpeechServiceCommand;
        public ICommand AddDeploymentCommand => _addDeploymentCommand;
        public ICommand RemoveDeploymentCommand => _removeDeploymentCommand;
        public ICommand AddRuleCommand => _addRuleCommand;
        public ICommand RemoveRuleCommand => _removeRuleCommand;
        public ICommand AddHotkeyRouteCommand => _addHotkeyRouteCommand;
        public ICommand RemoveHotkeyRouteCommand => _removeHotkeyRouteCommand;

        public void MarkDirty() => IsDirty = true;

        private void AttachChangeTracking(INotifyPropertyChanged? item)
        {
                if (item is null)
                        return;
                item.PropertyChanged += OnChildPropertyChanged;
        }

        private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
                MarkDirty();
                RecalculateValidation();
        }

        public SettingsModel BuildUpdatedSettings()
        {
                var clone = WorkingCopy.DeepClone();

                clone.SpeechToTextSettings ??= new SpeechToTextSettings();
                clone.SpeechToTextSettings.Services = SpeechServices.Select(s => s.ToSettings()).ToArray();

                clone.LlmSettings ??= new LlmSettings();
                clone.LlmSettings.ModelDeploymentIdMaps = ModelDeploymentMaps.Select(m => m.ToModel()).ToList();
                clone.LlmSettings.TranscriptFormatRules = TranscriptRules.Select(r => r.ToRule()).ToList();

                clone.HotKeyRouterSettings ??= new HotKeyRouterSettings();
                clone.HotKeyRouterSettings.Mappings = HotkeyRoutes.Select(r => r.ToMap()).ToList();

                return clone;
        }

        public void RecalculateValidation()
        {
                _validationErrors.Clear();

                if (string.IsNullOrWhiteSpace(WorkingCopy.SpeechToTextSettings?.TempDirectory))
                        _validationErrors.Add("Choose a temporary directory for recordings.");

                var duplicateServiceNames = SpeechServices
                        .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                        .GroupBy(s => s.Name!, StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();
                if (duplicateServiceNames.Count > 0)
                        _validationErrors.Add($"Duplicate speech service names: {string.Join(", ", duplicateServiceNames)}");

                foreach (var service in SpeechServices)
                {
                        var error = service.Validate();
                        if (!string.IsNullOrWhiteSpace(error))
                                _validationErrors.Add(error);
                }

                var duplicateRoutes = HotkeyRoutes
                        .Where(r => !string.IsNullOrWhiteSpace(r.FromHotkey))
                        .GroupBy(r => r.FromHotkey!, StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();
                if (duplicateRoutes.Count > 0)
                        _validationErrors.Add($"Duplicate router sources: {string.Join(", ", duplicateRoutes)}");

                foreach (var route in HotkeyRoutes)
                {
                        var error = route.Validate();
                        if (!string.IsNullOrWhiteSpace(error))
                                _validationErrors.Add(error);
                }

                foreach (var map in ModelDeploymentMaps)
                {
                        var error = map.Validate();
                        if (!string.IsNullOrWhiteSpace(error))
                                _validationErrors.Add(error);
                }

                foreach (var rule in TranscriptRules)
                {
                        var error = rule.Validate();
                        if (!string.IsNullOrWhiteSpace(error))
                                _validationErrors.Add(error);
                }

                OnPropertyChanged(nameof(ValidationErrors));
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(HasValidationErrors));
        }

        private void OnSelectedSectionChanged()
        {
                OnPropertyChanged(nameof(SelectedSection));
                NotifySectionVisibilityChanged();
        }

        private void NotifySectionVisibilityChanged()
        {
                OnPropertyChanged(nameof(GeneralSectionVisibility));
                OnPropertyChanged(nameof(AudioSectionVisibility));
                OnPropertyChanged(nameof(SpeechToTextSectionVisibility));
                OnPropertyChanged(nameof(LlmSectionVisibility));
                OnPropertyChanged(nameof(OcrSectionVisibility));
                OnPropertyChanged(nameof(TextToSpeechSectionVisibility));
                OnPropertyChanged(nameof(InterfaceSectionVisibility));
                OnPropertyChanged(nameof(HotkeyRouterSectionVisibility));

                OnPropertyChanged(nameof(IsGeneralSectionVisible));
                OnPropertyChanged(nameof(IsAudioSectionVisible));
                OnPropertyChanged(nameof(IsSpeechToTextSectionVisible));
                OnPropertyChanged(nameof(IsLlmSectionVisible));
                OnPropertyChanged(nameof(IsOcrSectionVisible));
                OnPropertyChanged(nameof(IsTextToSpeechSectionVisible));
                OnPropertyChanged(nameof(IsInterfaceSectionVisible));
                OnPropertyChanged(nameof(IsHotkeyRouterSectionVisible));
        }

        private bool IsSectionVisible(SettingsSection section) => SelectedSection == section;

        private Visibility GetSectionVisibility(SettingsSection section) =>
                IsSectionVisible(section) ? Visibility.Visible : Visibility.Collapsed;
}

internal sealed class SpeechServiceEntryViewModel : ObservableObject
{
        private readonly SettingsDialogViewModel _owner;
        private string? _name;
        private SpeechToTextProviders _provider;
        private string? _apiKey;
        private string? _baseDomain;
        private string? _modelId;
        private string? _prompt;
        private int _timeoutSeconds;

        public SpeechServiceEntryViewModel(SettingsDialogViewModel owner, SpeechToTextServiceSettings settings)
        {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _name = settings.Name;
                _provider = settings.Provider;
                _apiKey = settings.ApiKey;
                _baseDomain = settings.BaseDomain;
                _modelId = settings.ModelId;
                _prompt = settings.SpeechToTextPrompt;
                _timeoutSeconds = settings.TimeoutSeconds <= 0 ? 10 : settings.TimeoutSeconds;
        }

        public IReadOnlyList<SpeechToTextProviders> ProviderOptions => _owner.ProviderOptions;

        public ICommand RemoveCommand => _owner.RemoveSpeechServiceCommand;

        public string? Name
        {
                get => _name;
                set => SetProperty(ref _name, value);
        }

        public SpeechToTextProviders Provider
        {
                get => _provider;
                set => SetProperty(ref _provider, value);
        }

        public string? ApiKey
        {
                get => _apiKey;
                set => SetProperty(ref _apiKey, value);
        }

        public string? BaseDomain
        {
                get => _baseDomain;
                set => SetProperty(ref _baseDomain, value);
        }

        public string? ModelId
        {
                get => _modelId;
                set => SetProperty(ref _modelId, value);
        }

        public string? Prompt
        {
                get => _prompt;
                set => SetProperty(ref _prompt, value);
        }

        public double TimeoutSeconds
        {
                get => _timeoutSeconds;
                set
                {
                        int coerced = (int)Math.Max(1, Math.Round(value));
                        SetProperty(ref _timeoutSeconds, coerced);
                }
        }

        public SpeechToTextServiceSettings ToSettings() => new SpeechToTextServiceSettings
        {
                Name = Name,
                Provider = Provider,
                ApiKey = ApiKey,
                BaseDomain = BaseDomain,
                ModelId = ModelId,
                SpeechToTextPrompt = Prompt,
                TimeoutSeconds = _timeoutSeconds
        };

        public string? Validate()
        {
                if (string.IsNullOrWhiteSpace(Name))
                        return "Speech service names cannot be empty.";
                if (string.IsNullOrWhiteSpace(ModelId))
                        return $"Provide a model identifier for '{Name}'.";
                if (Provider == SpeechToTextProviders.OpenAi && string.IsNullOrWhiteSpace(ApiKey))
                        return $"Provide an API key for '{Name}'.";
                return null;
        }
}

internal sealed class ModelDeploymentMapViewModel : ObservableObject
{
        private readonly SettingsDialogViewModel _owner;
        private string? _modelName;
        private string? _deploymentId;

        public ModelDeploymentMapViewModel(SettingsDialogViewModel owner, LlmSettings.ModelDeploymentIdMap model)
        {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _modelName = model.ModelName;
                _deploymentId = model.DeploymentId;
        }

        public ICommand RemoveCommand => _owner.RemoveDeploymentCommand;

        public string? ModelName
        {
                get => _modelName;
                set => SetProperty(ref _modelName, value);
        }

        public string? DeploymentId
        {
                get => _deploymentId;
                set => SetProperty(ref _deploymentId, value);
        }

        public LlmSettings.ModelDeploymentIdMap ToModel() => new LlmSettings.ModelDeploymentIdMap(ModelName, DeploymentId);

        public string? Validate()
        {
                if (string.IsNullOrWhiteSpace(ModelName) && string.IsNullOrWhiteSpace(DeploymentId))
                        return null;
                if (string.IsNullOrWhiteSpace(ModelName) || string.IsNullOrWhiteSpace(DeploymentId))
                        return "Model mappings require both a model name and deployment id.";
                return null;
        }
}

internal sealed class TranscriptFormatRuleViewModel : ObservableObject
{
        private readonly SettingsDialogViewModel _owner;
        private string? _find;
        private string? _replaceWith;
        private bool _caseSensitive;
        private LlmSettings.TranscriptFormatRule.MatchTypeEnum _matchType;

        public TranscriptFormatRuleViewModel(SettingsDialogViewModel owner, LlmSettings.TranscriptFormatRule rule)
        {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _find = rule.Find;
                _replaceWith = rule.ReplaceWith;
                _caseSensitive = rule.CaseSensitive;
                _matchType = rule.MatchType;
        }

        public IReadOnlyList<LlmSettings.TranscriptFormatRule.MatchTypeEnum> MatchTypeOptions => _owner.MatchTypeOptions;

        public ICommand RemoveCommand => _owner.RemoveRuleCommand;

        public string? Find
        {
                get => _find;
                set => SetProperty(ref _find, value);
        }

        public string? ReplaceWith
        {
                get => _replaceWith;
                set => SetProperty(ref _replaceWith, value);
        }

        public bool CaseSensitive
        {
                get => _caseSensitive;
                set => SetProperty(ref _caseSensitive, value);
        }

        public LlmSettings.TranscriptFormatRule.MatchTypeEnum MatchType
        {
                get => _matchType;
                set => SetProperty(ref _matchType, value);
        }

        public LlmSettings.TranscriptFormatRule ToRule() => new LlmSettings.TranscriptFormatRule
        {
                Find = Find,
                ReplaceWith = ReplaceWith,
                CaseSensitive = CaseSensitive,
                MatchType = MatchType
        };

        public string? Validate()
        {
                if (string.IsNullOrWhiteSpace(Find) && string.IsNullOrWhiteSpace(ReplaceWith))
                        return null;
                if (string.IsNullOrWhiteSpace(Find))
                        return "Each transcript rule requires text to find.";
                return null;
        }
}

internal sealed class HotkeyRouteViewModel : ObservableObject
{
        private readonly SettingsDialogViewModel _owner;
        private string? _fromHotkey;
        private string? _toHotkey;

        public HotkeyRouteViewModel(SettingsDialogViewModel owner, HotKeyRouterSettings.HotKeyRouterMap map)
        {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _fromHotkey = map.FromHotKey;
                _toHotkey = map.ToHotKey;
        }

        public ICommand RemoveCommand => _owner.RemoveHotkeyRouteCommand;

        public string? FromHotkey
        {
                get => _fromHotkey;
                set => SetProperty(ref _fromHotkey, value);
        }

        public string? ToHotkey
        {
                get => _toHotkey;
                set => SetProperty(ref _toHotkey, value);
        }

        public HotKeyRouterSettings.HotKeyRouterMap ToMap() => new HotKeyRouterSettings.HotKeyRouterMap(FromHotkey ?? string.Empty, ToHotkey ?? string.Empty);

        public string? Validate()
        {
                        if (string.IsNullOrWhiteSpace(FromHotkey) && string.IsNullOrWhiteSpace(ToHotkey))
                                return null;
                        if (string.IsNullOrWhiteSpace(FromHotkey) || string.IsNullOrWhiteSpace(ToHotkey))
                                return "Hotkey router entries require both From and To values.";
                        return null;
        }
}
