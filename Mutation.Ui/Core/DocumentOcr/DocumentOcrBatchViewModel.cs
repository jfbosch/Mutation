using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CognitiveSupport.ComputerVision;

namespace Mutation.Ui.Core.DocumentOcr;

public sealed class DocumentOcrBatchViewModel : INotifyPropertyChanged
{
	private string? _statusSummary;
	private TimeSpan? _estimatedRemaining;
	private bool _completed;
	private bool _failed;
private string _aggregatedText = string.Empty;
private int _completedCount;
private int _failedCount;
private TimeSpan? _nextRetry;
private string? _errorSummary;
private string? _currentJobLabel;
private string? _nextRetryText;

public DocumentOcrBatchViewModel(Guid batchId, string sourceName)
{
BatchId = batchId;
SourceName = sourceName;
Jobs = new ObservableCollection<DocumentOcrJobViewModel>();
}

public Guid BatchId { get; }

	public string SourceName { get; }

	public ObservableCollection<DocumentOcrJobViewModel> Jobs { get; }

	public string? StatusSummary
	{
		get => _statusSummary;
		set
		{
			if (_statusSummary == value)
				return;
			_statusSummary = value;
			OnPropertyChanged();
		}
	}

	public TimeSpan? EstimatedRemaining
	{
		get => _estimatedRemaining;
		set
		{
			if (_estimatedRemaining == value)
				return;
			_estimatedRemaining = value;
			OnPropertyChanged();
		}
	}

	public bool Completed
	{
		get => _completed;
		set
		{
			if (_completed == value)
				return;
			_completed = value;
			OnPropertyChanged();
		}
	}

	public bool Failed
	{
		get => _failed;
		set
		{
			if (_failed == value)
				return;
			_failed = value;
			OnPropertyChanged();
		}
	}

public string AggregatedText
{
get => _aggregatedText;
		set
		{
			if (_aggregatedText == value)
				return;
			_aggregatedText = value ?? string.Empty;
			OnPropertyChanged();
		}
	}

	public int CompletedCount
	{
		get => _completedCount;
		set
		{
			if (_completedCount == value)
				return;
			_completedCount = value;
			OnPropertyChanged();
		}
	}

public int FailedCount
{
get => _failedCount;
set
{
if (_failedCount == value)
return;
_failedCount = value;
OnPropertyChanged();
}
}

public TimeSpan? NextRetry
{
get => _nextRetry;
set
{
if (_nextRetry == value)
return;
_nextRetry = value;
OnPropertyChanged();
}
}

public string? NextRetryText
{
get => _nextRetryText;
set
{
if (_nextRetryText == value)
return;
_nextRetryText = value;
OnPropertyChanged();
OnPropertyChanged(nameof(ShowNextRetry));
}
}

public bool ShowNextRetry => !string.IsNullOrWhiteSpace(NextRetryText);

public string? ErrorSummary
{
get => _errorSummary;
set
{
if (_errorSummary == value)
return;
_errorSummary = value;
OnPropertyChanged();
OnPropertyChanged(nameof(HasErrors));
}
}

public bool HasErrors => !string.IsNullOrWhiteSpace(ErrorSummary);

public string? CurrentJobLabel
{
get => _currentJobLabel;
set
{
if (_currentJobLabel == value)
return;
_currentJobLabel = value;
OnPropertyChanged();
OnPropertyChanged(nameof(ShowCurrentJob));
}
}

public bool ShowCurrentJob => !string.IsNullOrWhiteSpace(CurrentJobLabel);

public int TotalJobs => Jobs.Count;

public event PropertyChangedEventHandler? PropertyChanged;

public void RecalculateSummary()
{
CompletedCount = Jobs.Count(j => j.Status == DocumentOcrJobStatus.Completed);
FailedCount = Jobs.Count(j => j.Status == DocumentOcrJobStatus.Failed);
Completed = CompletedCount == Jobs.Count && Jobs.Count > 0;
Failed = FailedCount > 0;
StatusSummary = $"Completed {CompletedCount} / {Jobs.Count}";
NextRetry = CalculateNextRetry();
NextRetryText = NextRetry is null ? null : $"Next retry in {NextRetry.Value:mm\\:ss}";
ErrorSummary = BuildErrorSummary();
CurrentJobLabel = DetermineCurrentJob();
}

private TimeSpan? CalculateNextRetry()
{
TimeSpan? candidate = null;
foreach (var job in Jobs)
{
if (job.NextRetry is { } next)
{
if (candidate is null || next < candidate)
candidate = next;
}
}
return candidate;
}

private string? BuildErrorSummary()
{
var errors = Jobs.Where(j => !string.IsNullOrWhiteSpace(j.Error)).Select(j => $"{j.Label}: {j.Error}").ToList();
return errors.Count == 0 ? null : string.Join(" | ", errors);
}

private string? DetermineCurrentJob()
{
var active = Jobs.FirstOrDefault(j => j.Status is DocumentOcrJobStatus.Running or DocumentOcrJobStatus.Waiting);
if (active is not null)
return active.Label;
var queued = Jobs.FirstOrDefault(j => j.Status == DocumentOcrJobStatus.Queued);
return queued?.Label;
}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
