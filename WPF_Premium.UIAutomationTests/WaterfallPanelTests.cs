using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using WPF_Premium.Controls;

namespace WPF_Premium.UIAutomationTests;

[TestClass]
public sealed class WaterfallPanelTests
{
    [TestMethod]
    public void Places_each_child_in_the_shortest_column()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var panel = new WaterfallPanel
                {
                    ColumnCount = 2,
                    HorizontalSpacing = 10,
                    VerticalSpacing = 5
                };
                var first = new Border { Height = 100 };
                var second = new Border { Height = 60 };
                var third = new Border { Height = 30 };
                var fourth = new Border { Height = 30 };
                panel.Children.Add(first);
                panel.Children.Add(second);
                panel.Children.Add(third);
                panel.Children.Add(fourth);

                panel.Measure(new Size(210, double.PositiveInfinity));
                panel.Arrange(new Rect(0, 0, 210, 130));

                Assert.AreEqual(110, second.TranslatePoint(new Point(), panel).X, 0.1);
                Assert.AreEqual(65, third.TranslatePoint(new Point(), panel).Y, 0.1);
                Assert.AreEqual(100, fourth.TranslatePoint(new Point(), panel).Y, 0.1);
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
