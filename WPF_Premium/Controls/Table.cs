using System.Windows.Controls;

namespace WPF_Premium.Controls;

public sealed class Table : DataGrid
{
    public Table()
    {
        AutoGenerateColumns = false;
        CanUserAddRows = false;
        RowHeaderWidth = 0;
        HeadersVisibility = DataGridHeadersVisibility.Column;
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        CanUserResizeRows = false;
    }
}
