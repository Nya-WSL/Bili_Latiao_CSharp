using System.Windows;
using Bili_Latiao_CSharp.Services;

namespace Bili_Latiao_CSharp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static readonly string version = "1.0.0-alpha";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            LogService.RotateExistingLog();
        }

        public static void AddLog(string message)
        {
            if (Current.MainWindow is MainWindow win)
                win.ViewModel.AddLog(message);
        }
    }

}
