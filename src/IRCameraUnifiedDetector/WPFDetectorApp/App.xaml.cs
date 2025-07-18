using System.Windows;

namespace WPFDetectorApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // コマンドライン引数を解析
            var args = CommandLineArgs.Parse(e.Args);
            
            // MainWindowを作成して引数を渡す
            var mainWindow = new MainWindow(args);
            mainWindow.Show();
        }
    }
}