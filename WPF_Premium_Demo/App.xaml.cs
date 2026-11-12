using System.Configuration;
using System.Data;
using System.Windows;
using System.Globalization;

namespace WPF_Premium_Demo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            var vietnamese = CultureInfo.GetCultureInfo("vi-VN");
            CultureInfo.DefaultThreadCurrentCulture = vietnamese;
            CultureInfo.DefaultThreadCurrentUICulture = vietnamese;
            CultureInfo.CurrentCulture = vietnamese;
            CultureInfo.CurrentUICulture = vietnamese;
        }
    }

}
