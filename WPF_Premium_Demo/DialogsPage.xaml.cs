using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WPF_Premium.Controls;

namespace WPF_Premium_Demo;

public partial class DialogsPage : UserControl
{
    public DialogsPage()
    {
        InitializeComponent();
    }

    private void OpenDialog_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;

        var content = new TextBlock
        {
            Text = "Lớp phủ hộp thoại này được căn giữa cửa sổ cha. Bạn có thể đặt bất kỳ nội dung nào vào đây.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400
        };
        DialogService.ShowDialog(window, "Hộp thoại phương thức", content);
    }

    private void ShowConfirm_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;

        var content = new TextBlock
        {
            Text = "Bạn có chắc chắn muốn thực hiện hành động này?",
            TextWrapping = TextWrapping.Wrap
        };
        DialogService.ShowAlertDialog(window, "Xác nhận", content);
    }

    private void ShowDestructiveConfirm_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;

        var content = new TextBlock
        {
            Text = "Hành động này không thể hoàn tác. Tất cả dữ liệu sẽ bị xóa vĩnh viễn.",
            TextWrapping = TextWrapping.Wrap
        };
        DialogService.ShowAlertDialog(window, "Cảnh báo", content);
    }

    private void OpenPanel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string panelTag }) return;

        var window = Window.GetWindow(this);
        if (window == null) return;

        var panelParts = panelTag.Split(':', 2);
        var panelType = panelParts[0];
        var direction = panelParts.Length == 2
            ? Enum.Parse<OverlayDirection>(panelParts[1])
            : panelType == "Sheet" ? OverlayDirection.Bottom : OverlayDirection.Right;

        var directionText = direction switch
        {
            OverlayDirection.Left => "trái",
            OverlayDirection.Top => "trên",
            OverlayDirection.Right => "phải",
            OverlayDirection.Bottom => "dưới",
            _ => "phải"
        };

        var panelTitle = panelType switch
        {
            "Drawer" => "Ngăn trượt",
            "Sheet" => "Bảng trượt",
            _ => "Hộp thoại"
        };

        var content = CreateValidationForm();

        switch (panelType)
        {
            case "Drawer":
                DialogService.ShowDrawer(window, $"{panelTitle} - {directionText}", content, direction);
                break;
            case "Sheet":
                DialogService.ShowSheet(window, $"{panelTitle} - {directionText}", content, direction);
                break;
        }
    }

    private void ShowToast_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string toastType }) return;

        var window = Window.GetWindow(this);
        if (window == null) return;

        var kind = toastType switch
        {
            "Success" => ToastKind.Success,
            "Warning" => ToastKind.Default,
            "Error" => ToastKind.Destructive,
            "Info" => ToastKind.Default,
            _ => ToastKind.Default
        };

        ToastService.Show(window, $"Đây là thông báo {toastType.ToLower()}.", kind);
    }

    private UIElement CreateValidationForm()
    {
        var form = new StackPanel { Margin = new Thickness(2) };
        var nameInput = new TextBox { Tag = "Tên dự án" };
        var nameField = AddFormField(form, "Tên dự án", nameInput);
        var emailInput = new TextBox { Tag = "ten@vidu.vn" };
        var emailField = AddFormField(form, "Thư điện tử", emailInput);
        var countInput = new TextBox { Tag = "Số nguyên dương" };
        var countField = AddFormField(form, "Số lần thử lại", countInput);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
        var validateButton = new Button
        {
            Content = "Xác nhận",
            Style = (Style)FindResource("ButtonPrimary"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        validateButton.Click += (_, _) =>
        {
            nameField.Error = string.IsNullOrWhiteSpace(nameInput.Text) ? "Vui lòng nhập tên dự án." : null;
            emailField.Error = !emailInput.Text.Contains('@') ? "Vui lòng nhập địa chỉ thư điện tử hợp lệ." : null;
            countField.Error = !int.TryParse(countInput.Text, out var count) || count < 1
                ? "Vui lòng nhập số nguyên dương." : null;

            var isValid = new[] { nameField, emailField, countField }.All(f => !f.HasError);
            status.Text = isValid ? "Biểu mẫu hợp lệ." : "Vui lòng sửa các trường bị lỗi.";
            status.Foreground = (Brush)FindResource(isValid ? "PrimaryBrush" : "DestructiveBrush");
        };

        var layout = new StackPanel();
        layout.Children.Add(form);
        layout.Children.Add(status);
        layout.Children.Add(validateButton);
        return layout;
    }

    private Field AddFormField(Panel parent, string label, UIElement input)
    {
        var field = new Field
        {
            Label = label,
            IsRequired = true,
            Content = input,
            Style = (Style)FindResource("Field")
        };
        parent.Children.Add(field);
        return field;
    }
}