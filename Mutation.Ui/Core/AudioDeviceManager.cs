using CoreAudio;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mutation.Ui;

/// Manages enumeration and selection of audio capture devices as well as
/// mute and unmute operations.  This keeps audio related responsibilities
/// away from the WinForms logic in <see cref="MutationForm"/>.
public class AudioDeviceManager
{
        private readonly MMDeviceEnumerator _deviceEnumerator;
        private IList<MMDevice> _captureDevices = new List<MMDevice>();
        private MMDevice? _microphone;
        private int _microphoneDeviceIndex = -1;

        public AudioDeviceManager(MMDeviceEnumerator deviceEnumerator)
        {
                _deviceEnumerator = deviceEnumerator ?? throw new ArgumentNullException(nameof(deviceEnumerator));
                RefreshCaptureDevices();
        }

        public IEnumerable<MMDevice> CaptureDevices => _captureDevices;

        public MMDevice? Microphone => _microphone;

	public int MicrophoneDeviceIndex => _microphoneDeviceIndex;

        public bool IsMuted => _microphone != null && _microphone.AudioEndpointVolume.Mute;

        public void RefreshCaptureDevices()
        {
                var devices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                _captureDevices = devices.ToList();
        }

        public void SelectMicrophone(MMDevice device)
        {
                _microphone = device ?? throw new ArgumentNullException(nameof(device));
                SelectCaptureDeviceForNAudio();
        }

	public void EnsureDefaultMicrophoneSelected()
	{
		if (_microphone != null)
			return;

                try
                {
                        var defaultMic = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                        if (defaultMic != null)
                        {
                                _microphone = defaultMic;
                                SelectCaptureDeviceForNAudio();
                        }
                }
                catch { }
        }

	private void SelectCaptureDeviceForNAudio()
	{
		if (_microphone == null)
		{
			_microphoneDeviceIndex = -1;
			return;
		}

                string startsWithNameToMatch = $"{_microphone.FriendlyName} (";
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

	public void ToggleMute()
	{
		bool newMuteState = !IsMuted;
                foreach (var mic in _captureDevices)
                        mic.AudioEndpointVolume.Mute = newMuteState;
        }
}
