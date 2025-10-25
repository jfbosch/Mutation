using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CognitiveSupport.ComputerVision;

namespace Mutation.Ui.Core.DocumentOcr;

public sealed class DocumentOcrJobViewModel : INotifyPropertyChanged
{
	private DocumentOcrJobStatus _status;
	private double _progress;
	private TimeSpan? _nextRetry;
	private string? _error;
	private string? _extractedText;

	public DocumentOcrJobViewModel(Guid id, string label)
	{
		Id = id;
		Label = label;
	}

	public Guid Id { get; }

	public string Label { get; }

	public DocumentOcrJobStatus Status
	{
		get => _status;
		set
		{
			if (_status == value)
				return;
			_status = value;
			OnPropertyChanged();
		}
	}

	public double Progress
	{
		get => _progress;
		set
		{
			if (Math.Abs(_progress - value) < 0.0001)
				return;
			_progress = value;
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

	public string? Error
	{
		get => _error;
		set
		{
			if (_error == value)
				return;
			_error = value;
			OnPropertyChanged();
		}
	}

	public string? ExtractedText
	{
		get => _extractedText;
		set
		{
			if (_extractedText == value)
				return;
			_extractedText = value;
			OnPropertyChanged();
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
