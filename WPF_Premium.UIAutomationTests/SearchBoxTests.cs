using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MahApps.Metro.IconPacks;
using WPF_Premium.Controls;

namespace WPF_Premium.UIAutomationTests;

[TestClass]
public sealed class SearchBoxTests
{
    [TestMethod]
    public void Search_icon_shares_the_input_frame_and_clear_button_tracks_text()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var searchBox = new SearchBox { AutomationId = "SearchTestInput" };
                searchBox.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/WPF_Premium;component/Themes/WpfPremium.xaml", UriKind.Relative)
                });
                var frame = searchBox.Content as Border;
                Assert.IsNotNull(frame);
                var layout = frame.Child as Grid;
                Assert.IsNotNull(layout);

                var input = layout.Children.OfType<TextBox>().Single();
                var icon = layout.Children.OfType<PackIconMaterial>()
                    .SingleOrDefault(candidate => candidate.Kind == PackIconMaterialKind.Magnify);
                var clearButton = layout.Children.OfType<Button>().Single();

                Assert.IsNotNull(icon);
                Assert.AreEqual("SearchTestInput", AutomationProperties.GetAutomationId(input));
                Assert.AreEqual(Visibility.Collapsed, clearButton.Visibility);

                input.Text = "query";
                Assert.AreEqual("query", searchBox.Text);
                Assert.AreEqual(Visibility.Visible, clearButton.Visibility);

                clearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(string.Empty, searchBox.Text);
                Assert.AreEqual(Visibility.Collapsed, clearButton.Visibility);
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
}
