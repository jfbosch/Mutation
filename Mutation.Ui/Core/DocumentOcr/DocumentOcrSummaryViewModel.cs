using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CognitiveSupport.ComputerVision;

namespace Mutation.Ui.Core.DocumentOcr;

public sealed class DocumentOcrSummaryViewModel : INotifyPropertyChanged
{
	private int _totalDocuments;
	private int _completedDocuments;
	private int _failedDocuments;
	private string? _statusText;
	private TimeSpan? _estimatedRemaining;

	public int TotalDocuments
	{
		get => _totalDocuments;
		set
		{
			if (_totalDocuments == value)
				return;
			_totalDocuments = value;
			OnPropertyChanged();
		}
	}

	public int CompletedDocuments
	{
		get => _completedDocuments;
		set
		{
			if (_completedDocuments == value)
				return;
			_completedDocuments = value;
			OnPropertyChanged();
		}
	}

	public int FailedDocuments
	{
		get => _failedDocuments;
		set
		{
			if (_failedDocuments == value)
				return;
			_failedDocuments = value;
			OnPropertyChanged();
		}
	}

	public string? StatusText
	{
		get => _statusText;
		set
		{
			if (_statusText == value)
				return;
			_statusText = value;
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

	public event PropertyChangedEventHandler? PropertyChanged;

	public void UpdateFromBatches(IReadOnlyCollection<DocumentOcrBatchViewModel> batches)
	{
		TotalDocuments = batches.Count;
		CompletedDocuments = batches.Count(b => b.Completed);
		FailedDocuments = batches.Count(b => b.Failed);
		var totalRemaining = batches.Where(b => !b.Completed).Select(b => b.EstimatedRemaining ?? TimeSpan.Zero).DefaultIfEmpty(TimeSpan.Zero).Aggregate(TimeSpan.Zero, (a, b) => a + b);
		EstimatedRemaining = totalRemaining > TimeSpan.Zero ? totalRemaining : null;
		StatusText = $"Completed {CompletedDocuments} / {TotalDocuments} | Failures: {FailedDocuments}";
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
