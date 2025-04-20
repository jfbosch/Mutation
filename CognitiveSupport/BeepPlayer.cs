using System;
using System.IO;
using System.Media;

using CognitiveSupport;

namespace CognitiveSupport
{
	public enum BeepType
	{
		Start,
		Success,
		Failure,
		End
	}

	public static class BeepPlayer
	{
		// Default configuration (all values in Hertz and milliseconds)
		public const int DefaultStartFrequency = 970;
		public const int DefaultStartDuration = 80;

		// For Success, we play a sequence of two beeps.
		public static readonly (int Frequency, int Duration)[] DefaultSuccessSequence = new (int, int)[]
		{
			(1050, 40),
			(1150, 40)
		};

		public const int DefaultFailureFrequency = 300;
		public const int DefaultFailureDuration = 100;
		public const int DefaultFailureRepeats = 3;

		public const int DefaultEndFrequency = 800;
		public const int DefaultEndDuration = 50;

		private static SoundPlayer? _playerStart;
		private static SoundPlayer? _playerSuccess;
		private static SoundPlayer? _playerFailure;
		private static SoundPlayer? _playerEnd;

		// Call this on startup after the settings have been loaded.
		public static void Initialize ( Settings settings )
		{
			if ( settings.AudioSettings?.CustomBeepSettings != null &&
				settings.AudioSettings.CustomBeepSettings.UseCustomBeeps )
			{
				_playerStart = TryLoadPlayer ( settings.AudioSettings.CustomBeepSettings.BeepStartFile );
				_playerSuccess = TryLoadPlayer ( settings.AudioSettings.CustomBeepSettings.BeepSuccessFile );
				_playerFailure = TryLoadPlayer ( settings.AudioSettings.CustomBeepSettings.BeepFailureFile );
				_playerEnd = TryLoadPlayer ( settings.AudioSettings.CustomBeepSettings.BeepEmdFile );
			}
		}

		private static SoundPlayer? TryLoadPlayer ( string? filePath )
		{
			if ( !string.IsNullOrWhiteSpace ( filePath ) && File.Exists ( filePath ) )
			{
				try
				{
					var player = new SoundPlayer(filePath);
					player.Load ( );
					return player;
				}
				catch { }
			}
			return null;
		}

		public static void Play ( BeepType type )
		{
			// Play custom beep if available, otherwise use our defaults.
			switch ( type )
			{
				case BeepType.Start:
					if ( _playerStart != null )
					{
						_playerStart.Play ( );
						return;
					}
					break;
				case BeepType.Success:
					if ( _playerSuccess != null )
					{
						_playerSuccess.Play ( );
						return;
					}
					break;
				case BeepType.Failure:
					if ( _playerFailure != null )
					{
						_playerFailure.Play ( );
						return;
					}
					break;
				case BeepType.End:
					if ( _playerEnd != null )
					{
						_playerEnd.Play ( );
						return;
					}
					break;
			}

			// Otherwise, use our defaults:
			switch ( type )
			{
				case BeepType.Start:
					Console.Beep ( DefaultStartFrequency, DefaultStartDuration );
					break;
				case BeepType.Success:
					foreach ( var (frequency, duration) in DefaultSuccessSequence )
					{
						Console.Beep ( frequency, duration );
					}
					break;
				case BeepType.Failure:
					for ( int i = 0; i < DefaultFailureRepeats; i++ )
						Console.Beep ( DefaultFailureFrequency, DefaultFailureDuration );
					break;
				case BeepType.End:
					Console.Beep ( DefaultEndFrequency, DefaultEndDuration );
					break;
			}
		}
	}
}
