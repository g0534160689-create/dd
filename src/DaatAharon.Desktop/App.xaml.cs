using System.Windows;
using System.IO;

namespace DaatAharon.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception);
            MessageBox.Show("אירעה שגיאה לא צפויה. פרטי התקלה נשמרו ביומן האבחון.", "דעת אהרן", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        base.OnStartup(e);
    }

    private static void LogCrash(Exception? exception)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaatAharon", "Logs");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "startup-errors.log"),
                $"[{DateTimeOffset.Now:O}] {exception}\n{exception?.StackTrace}\n---\n");
        }
        catch { /* Diagnostics must never crash the application. */ }
    }
}
