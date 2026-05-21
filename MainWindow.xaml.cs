using System.Windows;
using System.Windows.Input;
using Bili_Latiao_CSharp.ViewModels;

namespace Bili_Latiao_CSharp
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            ViewModel = new MainViewModel();
            DataContext = ViewModel;
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
    }
}
