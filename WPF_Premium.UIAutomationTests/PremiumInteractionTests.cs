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
    private static readonly Lazy<Dispatcher> SharedStaDispatcher = new(StartStaThread);

    private static Dispatcher StartStaThread()
    {
        Dispatcher? dispatcher = null;
        var ready = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/WPF_Premium;component/Themes/WpfPremium.xaml", UriKind.Relative)
            });
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait();
        return dispatcher!;
    }

    [TestMethod]
    public void Pagination_command_runs_only_for_user_page_changes()
    {
        RunOnSta(() =>
        {
            var command = new RecordingCommand();
            var pagination = new Pagination
            {
                PageCount = 3,
                CurrentPage = 1,
                PageChangedCommand = command
            };
            var pagesPanel = (StackPanel)((StackPanel)pagination.Content).Children[0];
            var pageTwo = pagesPanel.Children
                .OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == "Trang 2");

            pageTwo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(2, pagination.CurrentPage);
            Assert.AreEqual(1, command.ExecutionCount);
            Assert.AreEqual(2, command.LastParameter);

            pagination.CurrentPage = 3;
            Assert.AreEqual(1, command.ExecutionCount);
        });
    }

    [TestMethod]
    public void Pagination_respects_can_go_previous_and_next_flags()
    {
        RunOnSta(() =>
        {
            var pagination = new Pagination
            {
                PageCount = 5,
                CurrentPage = 3,
                CanGoPreviousPage = false,
                CanGoNextPage = false
            };
            var pagesPanel = (StackPanel)((StackPanel)pagination.Content).Children[0];
            var prev = pagesPanel.Children.OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Trang trước");
            var next = pagesPanel.Children.OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Trang sau");

            Assert.IsFalse(prev.IsEnabled);
            Assert.IsFalse(next.IsEnabled);

            pagination.CanGoPreviousPage = true;
            pagination.CanGoNextPage = true;
            pagesPanel = (StackPanel)((StackPanel)pagination.Content).Children[0];
            prev = pagesPanel.Children.OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Trang trước");
            next = pagesPanel.Children.OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Trang sau");
            Assert.IsTrue(prev.IsEnabled);
            Assert.IsTrue(next.IsEnabled);
        });
    }

    [TestMethod]
    public void Rating_control_reuses_buttons_and_supports_allow_clear()
    {
        RunOnSta(() =>
        {
            var rating = new RatingControl { Maximum = 5, Value = 2, AllowClear = true };
            var panel = (StackPanel)rating.Content;
            var thirdStar = (Button)panel.Children[2];

            thirdStar.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(3, rating.Value);
            Assert.AreSame(thirdStar, panel.Children[2]);

            thirdStar.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, rating.Value);
            Assert.AreSame(thirdStar, panel.Children[2]);
        });
    }

    [TestMethod]
    public void Extended_controls_work_as_expected()
    {
        RunOnSta(() =>
        {
            var numeric = new NumericUpDown { Minimum = 10, Maximum = 50, Step = 5, Value = 200 };
            Assert.AreEqual(50, numeric.Value);
            numeric.Value = 2;
            Assert.AreEqual(10, numeric.Value);

            var segmented = new SegmentedControl { ItemsSource = new[] { "A", "B", "C" } };
            Assert.AreEqual(0, segmented.SelectedIndex);
            Assert.AreEqual("A", segmented.SelectedItem);
            segmented.SelectedIndex = 2;
            Assert.AreEqual("C", segmented.SelectedItem);

            var tagInput = new TagInput { MaxTags = 2, AllowDuplicates = false };
            Assert.IsTrue(tagInput.TryAddTag("WPF"));
            Assert.IsFalse(tagInput.TryAddTag("wpf"));
            Assert.IsTrue(tagInput.TryAddTag("UI"));
            Assert.IsFalse(tagInput.TryAddTag("Extra"));
            Assert.AreEqual(2, tagInput.Tags.Count);

            var banner = new BannerAlert { Title = "T", Message = "M", IsOpen = true };
            Assert.AreEqual(Visibility.Visible, banner.Visibility);
            banner.IsOpen = false;
            Assert.AreEqual(Visibility.Collapsed, banner.Visibility);

            var stat = new StatCard { Title = "Users", Value = "42", CornerRadius = new CornerRadius(12) };
            Assert.AreEqual(new CornerRadius(12), ((Border)stat.Content).CornerRadius);

            var rating = new RatingControl { StarSize = 28 };
            Assert.AreEqual(28d, rating.StarSize);

            var numericHiddenButtons = new NumericUpDown { ShowButtons = false, CornerRadius = new CornerRadius(8) };
            Assert.IsFalse(numericHiddenButtons.ShowButtons);
            Assert.AreEqual(new CornerRadius(8), ((Border)numericHiddenButtons.Content).CornerRadius);

            var app = Application.Current!;
            var accent = System.Windows.Media.Color.FromRgb(37, 99, 235);
            WPF_Premium.Themes.ThemeManager.SetAccentColor(app, accent);
            Assert.AreEqual(accent, ((System.Windows.Media.SolidColorBrush)app.Resources[WPF_Premium.Themes.WpfPremiumResourceKeys.PrimaryBrush]).Color);
            WPF_Premium.Themes.ThemeManager.ResetAccentColor(app);
            Assert.AreNotEqual(accent, ((System.Windows.Media.SolidColorBrush)app.Resources[WPF_Premium.Themes.WpfPremiumResourceKeys.PrimaryBrush]).Color);
        });
    }

    [TestMethod]
    public void FilePreview_supports_multichannel_wav_and_binary_telemetry_files()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wpf_premium_media_{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var stereoWav = System.IO.Path.Combine(tempDir, "test_stereo.wav");
            var quadWav = System.IO.Path.Combine(tempDir, "test_quad.wav");
            WriteTestWav(stereoWav, channels: 2, sampleRate: 16000, durationSeconds: 1.0);
            WriteTestWav(quadWav, channels: 4, sampleRate: 22050, durationSeconds: 0.5);

            var gpsidtFile = System.IO.Path.Combine(tempDir, "sample.gpsidt");
            var unkFile = System.IO.Path.Combine(tempDir, "sample.unk");
            var avocFile = System.IO.Path.Combine(tempDir, "sample.a_voc");
            var x3File = System.IO.Path.Combine(tempDir, "sample.x3");
            var f777File = System.IO.Path.Combine(tempDir, "sample.777");

            var payloadWith8F = new byte[72];
            System.Text.Encoding.ASCII.GetBytes("GPSIDT_VSAT_01").CopyTo(payloadWith8F, 0);
            payloadWith8F[16] = 0x8F;
            payloadWith8F[17] = 0x00;
            payloadWith8F[18] = 0xFE;
            System.Text.Encoding.ASCII.GetBytes("192.168.10.15").CopyTo(payloadWith8F, 24);

            foreach (var binPath in new[] { gpsidtFile, unkFile, avocFile, x3File, f777File })
            {
                System.IO.File.WriteAllBytes(binPath, payloadWith8F);
            }

            RunOnSta(() =>
            {
                var preview = new FilePreviewControl { Width = 900, Height = 500 };

                preview.LoadFile(stereoWav);
                var waveElement = FindLogicalDescendants(preview).OfType<FrameworkElement>()
                    .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "AudioWaveform");
                Assert.IsNotNull(waveElement);
                var rangeToggle = FindLogicalDescendants(preview).OfType<System.Windows.Controls.Primitives.ToggleButton>()
                    .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "AudioRangeSelectToggle");
                Assert.IsNotNull(rangeToggle);
                var zoomText = FindLogicalDescendants(preview).OfType<TextBlock>()
                    .FirstOrDefault(e => e.Name == "ZoomText");
                Assert.IsNotNull(zoomText);
                StringAssert.Contains(zoomText.Text, "2 kênh");

                preview.LoadFile(quadWav);
                StringAssert.Contains(zoomText.Text, "4 kênh");

                foreach (var binPath in new[] { gpsidtFile, unkFile, avocFile, x3File, f777File })
                {
                    preview.LoadFile(binPath);
                    var error = FindLogicalDescendants(preview).OfType<FrameworkElement>()
                        .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "PreviewLoadError");
                    Assert.IsNull(error, $"Unexpected preview error for {binPath}");

                    var textBox = FindLogicalDescendants(preview).OfType<TextBox>()
                        .FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "PreviewTextContent");
                    Assert.IsNotNull(textBox, $"Missing text/hex preview for {binPath}");
                    StringAssert.Contains(textBox.Text, "Nội dung Hex / ASCII:");
                    StringAssert.Contains(textBox.Text, "192.168.10.15");
                }
            });
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static void WriteTestWav(string path, short channels, int sampleRate, double durationSeconds)
    {
        var totalFrames = (int)(sampleRate * durationSeconds);
        short bitsPerSample = 16;
        short blockAlign = (short)(channels * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;
        var dataSize = totalFrames * blockAlign;

        using var fs = System.IO.File.Create(path);
        using var bw = new System.IO.BinaryWriter(fs);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write(channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write(bitsPerSample);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);

        for (var f = 0; f < totalFrames; f++)
        {
            for (var ch = 0; ch < channels; ch++)
            {
                var freq = 220.0 * (ch + 1);
                var amp = 0.25 + 0.15 * ch;
                var sample = (short)(Math.Sin(2 * Math.PI * freq * f / sampleRate) * amp * short.MaxValue);
                bw.Write(sample);
            }
        }
    }

    [TestMethod]
    public void Choice_dialog_returns_the_selected_choice_id()
    {
        RunOnSta(() =>
        {
            var application = Application.Current!;
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
            }
        });
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

    internal static void RunOnSta(Action action)
    {
        Exception? failure = null;
        SharedStaDispatcher.Value.Invoke(() =>
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
