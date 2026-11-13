using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

public enum StepState
{
    Upcoming,
    Current,
    Complete
}

public class StepBar : ItemsControl
{
    public static readonly DependencyProperty CurrentStepProperty =
        DependencyProperty.Register(
            nameof(CurrentStep),
            typeof(int),
            typeof(StepBar),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnCurrentStepChanged),
            value => (int)value >= 0);

    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    protected override DependencyObject GetContainerForItemOverride() => new StepBarItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is StepBarItem;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is StepBarItem container)
        {
            UpdateContainer(container, ItemContainerGenerator.IndexFromContainer(container));
        }
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        UpdateContainers();
    }

    private static void OnCurrentStepChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e) =>
        ((StepBar)dependencyObject).UpdateContainers();

    private void UpdateContainers()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(index) is StepBarItem container)
            {
                UpdateContainer(container, index);
            }
        }
    }

    private void UpdateContainer(StepBarItem container, int index)
    {
        if (index < 0)
        {
            return;
        }

        container.StepIndex = index;
        container.MarkerText = (index + 1).ToString();
        container.IsFirst = index == 0;
        container.IsLast = index == Items.Count - 1;
        container.State = index < CurrentStep
            ? StepState.Complete
            : index == CurrentStep ? StepState.Current : StepState.Upcoming;
    }
}

public class StepBarItem : ContentControl
{
    public static readonly DependencyProperty StepIndexProperty =
        DependencyProperty.Register(nameof(StepIndex), typeof(int), typeof(StepBarItem), new PropertyMetadata(0));
    public static readonly DependencyProperty MarkerTextProperty =
        DependencyProperty.Register(nameof(MarkerText), typeof(string), typeof(StepBarItem), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty IsFirstProperty =
        DependencyProperty.Register(nameof(IsFirst), typeof(bool), typeof(StepBarItem), new PropertyMetadata(false));
    public static readonly DependencyProperty IsLastProperty =
        DependencyProperty.Register(nameof(IsLast), typeof(bool), typeof(StepBarItem), new PropertyMetadata(false));
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(StepState), typeof(StepBarItem), new PropertyMetadata(StepState.Upcoming));

    public int StepIndex
    {
        get => (int)GetValue(StepIndexProperty);
        set => SetValue(StepIndexProperty, value);
    }

    public string MarkerText
    {
        get => (string)GetValue(MarkerTextProperty);
        set => SetValue(MarkerTextProperty, value);
    }

    public bool IsFirst
    {
        get => (bool)GetValue(IsFirstProperty);
        set => SetValue(IsFirstProperty, value);
    }

    public bool IsLast
    {
        get => (bool)GetValue(IsLastProperty);
        set => SetValue(IsLastProperty, value);
    }

    public StepState State
    {
        get => (StepState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }
}
