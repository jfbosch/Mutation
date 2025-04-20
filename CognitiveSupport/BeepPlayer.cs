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
		public const int DefaultStartFrequency = 970;
		public const int DefaultStartDuration = 80;

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
		public static IReadOnlyList<string> LastInitializationIssues { get; private set; } = Array.Empty<string> ( );

		public static void Initialize ( Settings settings )
		{
			var issues = new List<string>();

			if ( settings.AudioSettings?.CustomBeepSettings != null &&
				settings.AudioSettings.CustomBeepSettings.UseCustomBeeps )
			{
				_playerStart = TryLoadPlayer ( settings.AudioSettings.CustomBeepSettings.BeepStartFile );
				if ( _playerStart == null && !string.IsNullOrWhiteSpace ( settings.AudioSettings.CustomBeepSettings.BeepStartFile ) )
					issues.Add ( $"Could not load start beep file: {settings.AudioSettings.CustomBeepSettings.BeepStartFile}" );

				_playerSuccess = TryLoadPlayer ( settings.AudioSettings.CustomBeepSettings.BeepSuccessFile );
				if ( _playerSuccess == null && !string.IsNullOrWhiteSpace ( settings.AudioSettings.CustomBeepSettings.BeepSuccessFile ) )
					issues.Add ( $"Could not load success beep file: {settings.AudioSettings.CustomBeepSettings.BeepSuccessFile}" );

				_playerFailure = TryLoadPlayer ( settings.AudioSettings.CustomBeepSettings.BeepFailureFile );
				if ( _playerFailure == null && !string.IsNullOrWhiteSpace ( settings.AudioSettings.CustomBeepSettings.BeepFailureFile ) )
					issues.Add ( $"Could not load failure beep file: {settings.AudioSettings.CustomBeepSettings.BeepFailureFile}" );

				_playerEnd = TryLoadPlayer ( settings.AudioSettings.CustomBeepSettings.BeepEndFile );
				if ( _playerEnd == null && !string.IsNullOrWhiteSpace ( settings.AudioSettings.CustomBeepSettings.BeepEndFile ) )
					issues.Add ( $"Could not load end beep file: {settings.AudioSettings.CustomBeepSettings.BeepEndFile}" );
			}

			LastInitializationIssues = issues;
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
