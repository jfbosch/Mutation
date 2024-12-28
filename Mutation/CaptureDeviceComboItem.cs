using AudioSwitcher.AudioApi.CoreAudio;

namespace Mutation
{
	internal class CaptureDeviceComboItem
	{
		public CoreAudioDevice CaptureDevice { get; set; }
		public string Id { get; set; }
		public string Display =>
			$"{CaptureDevice.FullName}";

		public override string ToString()
		{
			return this.Display;
		}
	}
}
