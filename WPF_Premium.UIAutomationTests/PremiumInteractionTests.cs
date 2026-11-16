using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WPF_Premium.Controls;

namespace WPF_Premium.UIAutomationTests;

[TestClass]
public sealed class PremiumInteractionTests
{
    [TestMethod]
    public void Pagination_command_runs_only_for_user_page_changes()
    {
        RunOnSta(() =>
        {
            var application = CreateApplication();
            try
            {
                var command = new RecordingCommand();
                var pagination = new Pagination
                {
                    PageCount = 3,
                    CurrentPage = 1,
                    PageChangedCommand = command
                };
                var pageTwo = ((StackPanel)pagination.Content).Children
                    .OfType<Button>()
                    .Single(button => AutomationProperties.GetName(button) == "Trang 2");

                pageTwo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(2, pagination.CurrentPage);
                Assert.AreEqual(1, command.ExecutionCount);
                Assert.AreEqual(2, command.LastParameter);

                pagination.CurrentPage = 3;
                Assert.AreEqual(1, command.ExecutionCount);
            }
            finally
            {
                application.Shutdown();
            }
        });
    }

    [TestMethod]
    public void Choice_dialog_returns_the_selected_choice_id()
    {
        RunOnSta(() =>
        {
            var application = CreateApplication();
            var owner = new Window { Width = 480, Height = 320, ShowInTaskbar = false };
            try
            {
                application.MainWindow = owner;
                owner.Show();

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    var overlay = application.Windows.OfType<Window>()
                        .Single(window => !ReferenceEquals(window, owner) && window.IsVisible);
                    var choiceButton = FindLogicalDescendants(overlay)
                        .OfType<Button>()
                        .Single(button => button.Content as string == "Chọn kết nối");
                    choiceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };
                timer.Start();

                var selectedChoice = DialogService.ShowChoiceDialog(
                    owner,
                    "Kết nối thất bại",
                    new TextBlock { Text = "Chọn thao tác." },
                    [
                        new DialogChoice("retry", "Thử lại", DialogChoiceVariant.Primary),
                        new DialogChoice("change", "Chọn kết nối", DialogChoiceVariant.Outline),
                        new DialogChoice("cancel", "Hủy", DialogChoiceVariant.Ghost)
                    ]);

                Assert.AreEqual("change", selectedChoice);
            }
            finally
            {
                owner.Close();
                application.Shutdown();
            }
        });
    }

    private static Application CreateApplication()
    {
        var application = new Application();
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/WPF_Premium;component/Themes/WpfPremium.xaml", UriKind.Relative)
        });
        return application;
    }

    private static IEnumerable<DependencyObject> FindLogicalDescendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in FindLogicalDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class RecordingCommand : ICommand
    {
        public int ExecutionCount { get; private set; }
        public object? LastParameter { get; private set; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter)
        {
            ExecutionCount++;
            LastParameter = parameter;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
