namespace WPF_Premium.Controls;

public enum ChartKind
{
    Line,
    Area,
    Column,
    Bar,
    StackedColumn,
    StackedBar,
    StepLine,
    Scatter,
    Pie,
    Doughnut
}

public sealed record ChartSeries
{
    public ChartSeries(string name, IReadOnlyList<double> values, IReadOnlyList<double>? xValues = null)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A chart series name is required.", nameof(name))
            : name;
        Values = values ?? throw new ArgumentNullException(nameof(values));
        if (xValues is not null && xValues.Count != values.Count)
        {
            throw new ArgumentException("X and Y values must have the same number of points.", nameof(xValues));
        }

        XValues = xValues;
    }

    public string Name { get; }

    public IReadOnlyList<double> Values { get; }

    public IReadOnlyList<double>? XValues { get; }
}
