using CoreAudio;

namespace Mutation.Ui;

internal class CaptureDeviceComboItem
{
	public MMDevice CaptureDevice { get; set; }
	public string Display =>
			  $"{CaptureDevice.FriendlyName}";

	public override string ToString()
	{
		return this.Display;
	}
}
