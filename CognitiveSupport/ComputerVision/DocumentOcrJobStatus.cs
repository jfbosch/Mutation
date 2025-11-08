namespace CognitiveSupport.ComputerVision;

/// <summary>
/// Represents the states returned by document OCR jobs.
/// </summary>
public enum DocumentOcrJobStatus
{
	Queued,
	Running,
	Waiting,
	Completed,
	Failed
}
