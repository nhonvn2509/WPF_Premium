using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public enum TraceEntryKind
{
    Thought,
    Plan,
    Queue,
    Reasoning,
    Sources,
    StackTrace,
    Subagent,
    Task,
    Terminal,
    TestResults,
    Tool,
    ChainOfThought,
    Error
}

public sealed class TraceEntry : INotifyPropertyChanged
{
    private TraceEntryKind _kind = TraceEntryKind.Task;
    private PackIconMaterialKind? _icon;
    private string _title = string.Empty;
    private string? _description;
    private string? _status;
    private string? _details;
    private string? _duration;
    private string? _command;
    private string? _output;
    private string? _navigationLabel;
    private string? _actionLabel;
    private ICommand? _navigationCommand;
    private ICommand? _actionCommand;
    private bool _isExpanded;
    private bool _isActive;
    private int? _addedLines;
    private int? _removedLines;

    public TraceEntry()
    {
        Sources.CollectionChanged += ExpandableItems_CollectionChanged;
        Children.CollectionChanged += ExpandableItems_CollectionChanged;
    }

    public TraceEntryKind Kind
    {
        get => _kind;
        set
        {
            if (SetProperty(ref _kind, value))
            {
                OnPropertyChanged(nameof(DisplayIcon));
                OnPropertyChanged(nameof(IsError));
            }
        }
    }

    public PackIconMaterialKind? Icon
    {
        get => _icon;
        set
        {
            if (SetProperty(ref _icon, value))
            {
                OnPropertyChanged(nameof(DisplayIcon));
            }
        }
    }

    public PackIconMaterialKind DisplayIcon => Icon ?? Kind switch
    {
        TraceEntryKind.Thought or TraceEntryKind.Reasoning or TraceEntryKind.ChainOfThought => PackIconMaterialKind.LightbulbOutline,
        TraceEntryKind.Plan or TraceEntryKind.Queue or TraceEntryKind.Task => PackIconMaterialKind.FormatListChecks,
        TraceEntryKind.Sources => PackIconMaterialKind.Web,
        TraceEntryKind.StackTrace or TraceEntryKind.Error => PackIconMaterialKind.AlertCircleOutline,
        TraceEntryKind.Subagent => PackIconMaterialKind.AccountOutline,
        TraceEntryKind.Terminal => PackIconMaterialKind.ConsoleLine,
        TraceEntryKind.Tool => PackIconMaterialKind.WrenchOutline,
        TraceEntryKind.TestResults => PackIconMaterialKind.CheckCircleOutline,
        _ => PackIconMaterialKind.CircleSmall
    };

    public string Title { get => _title; set => SetProperty(ref _title, value); }

    public string? Description
    {
        get => _description;
        set => SetExpandableProperty(ref _description, value);
    }

    public string? Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsError));
            }
        }
    }

    public string? Details
    {
        get => _details;
        set => SetExpandableProperty(ref _details, value);
    }

    public string? Duration { get => _duration; set => SetProperty(ref _duration, value); }

    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    public int? AddedLines { get => _addedLines; set => SetProperty(ref _addedLines, value); }
    public int? RemovedLines { get => _removedLines; set => SetProperty(ref _removedLines, value); }

    public string? Command
    {
        get => _command;
        set => SetExpandableProperty(ref _command, value);
    }

    public string? Output
    {
        get => _output;
        set => SetExpandableProperty(ref _output, value);
    }

    public string? NavigationLabel
    {
        get => _navigationLabel;
        set
        {
            if (SetProperty(ref _navigationLabel, value))
            {
                OnPropertyChanged(nameof(HasDetails));
            }
        }
    }

    public string? ActionLabel
    {
        get => _actionLabel;
        set
        {
            if (SetProperty(ref _actionLabel, value))
            {
                OnPropertyChanged(nameof(HasDetails));
            }
        }
    }

    public ICommand? NavigationCommand
    {
        get => _navigationCommand;
        set
        {
            if (SetProperty(ref _navigationCommand, value))
            {
                OnPropertyChanged(nameof(HasDetails));
            }
        }
    }

    public ICommand? ActionCommand
    {
        get => _actionCommand;
        set
        {
            if (SetProperty(ref _actionCommand, value))
            {
                OnPropertyChanged(nameof(HasDetails));
            }
        }
    }

    public ObservableCollection<TraceSource> Sources { get; } = new();
    public ObservableCollection<TraceEntry> Children { get; } = new();

    public bool HasDetails =>
        !string.IsNullOrWhiteSpace(Description) ||
        !string.IsNullOrWhiteSpace(Details) ||
        !string.IsNullOrWhiteSpace(Command) ||
        !string.IsNullOrWhiteSpace(Output) ||
        Sources.Count > 0 ||
        Children.Count > 0 ||
        NavigationCommand is not null ||
        ActionCommand is not null;

    public bool IsError =>
        Kind is TraceEntryKind.Error or TraceEntryKind.StackTrace ||
        string.Equals(Status, "Failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "Error", StringComparison.OrdinalIgnoreCase);

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetExpandableProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        OnPropertyChanged(nameof(HasDetails));
        return true;
    }

    private void ExpandableItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasDetails));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class TraceSource
{
    public string Title { get; set; } = string.Empty;
    public string? Uri { get; set; }
    public string? Description { get; set; }
    public ICommand? OpenCommand { get; set; }
}
