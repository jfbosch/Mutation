using CoreAudio;

namespace Mutation.Ui;

internal class CaptureDeviceComboItem
{
	public MMDevice? CaptureDevice { get; set; }
	public string Display
	{
		get
		{
#pragma warning disable CS0618 // Support fallback if DeviceFriendlyName not populated
			if (CaptureDevice == null)
				return string.Empty;
			var friendly = CaptureDevice.DeviceFriendlyName;
			if (string.IsNullOrWhiteSpace(friendly))
				friendly = CaptureDevice.FriendlyName;
#pragma warning restore CS0618
			return friendly ?? string.Empty;
		}
	}

	public override string ToString() => Display;
}
