# Kiểm thử giao diện bằng FlaUI

Dự án này khởi chạy ứng dụng demo WPF thật và dùng FlaUI/UIA3 để kiểm tra điều khiển xem trước tệp. Mỗi ca tạo tệp mẫu tạm, mở ứng dụng với tham số `--preview`, xác minh đúng nhóm công cụ xem trước và các thao tác mở bằng ứng dụng mặc định/mở thư mục.

Chạy toàn bộ 36 ca trên Windows có phiên desktop tương tác:

```powershell
dotnet test WPF_Premium.UIAutomationTests/WPF_Premium.UIAutomationTests.csproj --configuration Debug
```

Có thể giới hạn chạy theo phần mở rộng bằng biến môi trường `WPF_PREVIEW_TEST_EXTENSIONS`:

```powershell
$env:WPF_PREVIEW_TEST_EXTENSIONS = ".png,.pdf,.docx,.mp4"
dotnet test WPF_Premium.UIAutomationTests/WPF_Premium.UIAutomationTests.csproj --configuration Debug
Remove-Item Env:WPF_PREVIEW_TEST_EXTENSIONS
```

Các nhóm được kiểm tra gồm ảnh (`png`, `jpg`, `jpeg`, `bmp`, `gif`, `tif`, `tiff`), âm thanh (`mp3`, `wav`, `wma`, `m4a`, `aac`, `mid`, `midi`), video (`mp4`, `wmv`, `avi`, `mpeg`, `mpg`, `mov`) và nội dung văn bản/tài liệu (`txt`, `csv`, `xml`, `md`, `log`, `json`, `eml`, `pdf`, `docx`, `xlsx`, `xlsm`, `doc`, `xls`, `ppt`, `pptx`, `zip`).

Các ca Office cũ (`doc`, `xls`, `ppt`, `pptx`) xác nhận thông báo giới hạn xem trước hiện có; các định dạng Office được hỗ trợ trích nội dung (`docx`, `xlsx`, `xlsm`) kiểm tra chuỗi nội dung mẫu.
