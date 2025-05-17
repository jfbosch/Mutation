using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using NAudio.Wave;

namespace Mutation;

/// <summary>
/// Manages enumeration and selection of audio capture devices as well as
/// mute and unmute operations.  This keeps audio related responsibilities
/// away from the WinForms logic in <see cref="MutationForm"/>.
/// </summary>
public class AudioDeviceManager
{
	private readonly CoreAudioController _controller;
	private IEnumerable<CoreAudioDevice> _captureDevices = Enumerable.Empty<CoreAudioDevice>();
	private CoreAudioDevice? _microphone;
	private int _microphoneDeviceIndex = -1;

	public AudioDeviceManager(CoreAudioController controller)
	{
		_controller = controller ?? throw new ArgumentNullException(nameof(controller));
		RefreshCaptureDevices();
	}

	/// <summary>
	/// List of active capture devices.
	/// </summary>
	public IEnumerable<CoreAudioDevice> CaptureDevices => _captureDevices;

	/// <summary>
	/// The currently selected microphone device.
	/// </summary>
	public CoreAudioDevice? Microphone => _microphone;

	/// <summary>
	/// Gets the NAudio device index for the selected microphone.
	/// </summary>
	public int MicrophoneDeviceIndex => _microphoneDeviceIndex;

	/// <summary>
	/// Indicates if the microphone is muted.
	/// </summary>
	public bool IsMuted => _microphone != null && _microphone.IsMuted;

	/// <summary>
	/// Refreshes the list of capture devices from the system.
	/// </summary>
	public void RefreshCaptureDevices()
	{
		_captureDevices = _controller.GetDevices(DeviceType.Capture, DeviceState.Active);
	}

	/// <summary>
	/// Sets the microphone to the specified device.
	/// </summary>
	public void SelectMicrophone(CoreAudioDevice device)
	{
		_microphone = device ?? throw new ArgumentNullException(nameof(device));
		SelectCaptureDeviceForNAudio();
	}

	/// <summary>
	/// Attempts to select the default system capture device if no microphone
	/// has been selected yet.
	/// </summary>
	public void EnsureDefaultMicrophoneSelected()
	{
		if (_microphone != null)
			return;

		var defaultMic = _captureDevices.FirstOrDefault(d => d.IsDefaultDevice);
		if (defaultMic != null)
		{
			_microphone = defaultMic;
			SelectCaptureDeviceForNAudio();
		}
	}

	private void SelectCaptureDeviceForNAudio()
	{
		if (_microphone == null)
		{
			_microphoneDeviceIndex = -1;
			return;
		}

		string startsWithNameToMatch = $"{_microphone.Name} (";
		int deviceCount = WaveIn.DeviceCount;
		for (int i = 0; i < deviceCount; i++)
		{
			if (WaveInEvent.GetCapabilities(i).ProductName.StartsWith(startsWithNameToMatch))
			{
				_microphoneDeviceIndex = i;
				return;
			}
		}
		_microphoneDeviceIndex = -1;
	}

	/// <summary>
	/// Toggles the mute state for all detected capture devices.
	/// </summary>
	public void ToggleMute()
	{
		bool newMuteState = !IsMuted;
		foreach (var mic in _captureDevices)
			mic.Mute(newMuteState);
	}
}
