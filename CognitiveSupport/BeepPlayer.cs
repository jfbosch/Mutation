﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace CognitiveSupport
{
	public enum BeepType { Start, Success, Failure, End, Mute, Unmute }

	public static class BeepPlayer
	{
		public const int DefaultStartFrequency = 970;
		public const int DefaultStartDuration = 80;
		public static readonly (int Frequency,int Duration)[] DefaultSuccessSequence = new[] { (1050,40),(1150,40) };
		public const int DefaultFailureFrequency = 300;
		public const int DefaultFailureDuration = 100;
		public const int DefaultFailureRepeats = 3;
		public const int DefaultEndFrequency = 800;
		public const int DefaultEndDuration = 50;
		public const int DefaultMuteFrequency = 500;
		public const int DefaultMuteDuration = 200;
		public const int DefaultUnmuteFrequency = 1300;
		public const int DefaultUnmuteDuration = 50;

		private static readonly object SyncLock = new();
		private static SoundPlayer? _playerStart;
		private static SoundPlayer? _playerSuccess;
		private static SoundPlayer? _playerFailure;
		private static SoundPlayer? _playerEnd;
		private static SoundPlayer? _playerMute;
		private static SoundPlayer? _playerUnmute;
		public static IReadOnlyList<string> LastInitializationIssues { get; private set; } = Array.Empty<string> ( );

		public static void Initialize ( Settings settings )
		{
			lock ( SyncLock )
			{
				DisposePlayers ( );
				var issues = new List<string>();
				var custom = settings.AudioSettings?.CustomBeepSettings;
				if ( custom?.UseCustomBeeps == true )
				{
					_playerStart = LoadPlayer ( custom.BeepStartFile, fp => issues.Add ( $"Could not load start beep file: {fp}" ) );
					_playerSuccess = LoadPlayer ( custom.BeepSuccessFile, fp => issues.Add ( $"Could not load success beep file: {fp}" ) );
					_playerFailure = LoadPlayer ( custom.BeepFailureFile, fp => issues.Add ( $"Could not load failure beep file: {fp}" ) );
					_playerEnd = LoadPlayer ( custom.BeepEndFile, fp => issues.Add ( $"Could not load end beep file: {fp}" ) );
					_playerMute = LoadPlayer ( custom.BeepMuteFile, fp => issues.Add ( $"Could not load mute beep file: {fp}" ) );
					_playerUnmute = LoadPlayer ( custom.BeepUnmuteFile, fp => issues.Add ( $"Could not load unmute beep file: {fp}" ) );
				}
				LastInitializationIssues = issues;
			}
		}

		private static SoundPlayer? LoadPlayer ( string filePath, Action<string> onError )
		{
			if ( string.IsNullOrWhiteSpace ( filePath ) || !File.Exists ( filePath ) )
				return null;
			try
			{
				var player = new SoundPlayer(filePath);
				player.Load ( );
				return player;
			}
			catch
			{
				onError ( filePath );
				return null;
			}
		}

		public static void Play ( BeepType type )
		{
			if ( TryPlayCustom ( type ) )
				return;
			PlayDefault ( type );
		}

		private static bool TryPlayCustom ( BeepType type )
		{
			var player = type switch
			{
				BeepType.Start => _playerStart,
				BeepType.Success => _playerSuccess,
				BeepType.Failure => _playerFailure,
				BeepType.End => _playerEnd,
				BeepType.Mute => _playerMute,
				BeepType.Unmute => _playerUnmute,
				_ => null
			};
			if ( player != null )
			{
				player.Play ( );
				return true;
			}
			return false;
		}

		private static void PlayDefault ( BeepType type )
		{
			switch ( type )
			{
				case BeepType.Start:
					Console.Beep ( DefaultStartFrequency, DefaultStartDuration );
					break;
				case BeepType.Success:
					foreach ( var (frequency, duration) in DefaultSuccessSequence )
						Console.Beep ( frequency, duration );
					break;
				case BeepType.Failure:
					for ( var i = 0; i < DefaultFailureRepeats; i++ )
						Console.Beep ( DefaultFailureFrequency, DefaultFailureDuration );
					break;
				case BeepType.End:
					Console.Beep ( DefaultEndFrequency, DefaultEndDuration );
					break;
				case BeepType.Mute:
					Console.Beep ( DefaultMuteFrequency, DefaultMuteDuration );
					break;
				case BeepType.Unmute:
					Console.Beep ( DefaultUnmuteFrequency, DefaultUnmuteDuration );
					break;
			}
		}

		public static void DisposePlayers ( )
		{
			_playerStart?.Dispose ( );
			_playerSuccess?.Dispose ( );
			_playerFailure?.Dispose ( );
			_playerEnd?.Dispose ( );
			_playerMute?.Dispose ( );
			_playerUnmute?.Dispose ( );
			_playerStart = null;
			_playerSuccess = null;
			_playerFailure = null;
			_playerEnd = null;
			_playerMute = null;
			_playerUnmute = null;
		}
	}
}
