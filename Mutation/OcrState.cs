﻿using CognitiveSupport;

namespace Mutation
{
	internal class OcrState
	{
		internal bool BusyWithTextExtraction => OcrCancellationTokenSource != null;
		internal CancellationTokenSource? OcrCancellationTokenSource { get; set; }

		public OcrState()
		{
		}

		internal void StartTextExtraction()
		{
			this.OcrCancellationTokenSource = new();
		}

		internal void StopTextExtraction()
		{
			if (this.OcrCancellationTokenSource is not null)
				this.OcrCancellationTokenSource.Cancel();
			this.OcrCancellationTokenSource = null;
		}


	}
}
