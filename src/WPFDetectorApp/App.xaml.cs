using System.Windows;

namespace WPFDetectorApp;

public partial class App : Application
{
  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    // コマンドライン引数を解析
    CommandLineArgs args = CommandLineArgs.Parse(e.Args);

    // MainWindowを作成して引数を渡す
    MainWindow mainWindow = new(args);
    mainWindow.Show();
  }
}
