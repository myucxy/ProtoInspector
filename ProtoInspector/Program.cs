using System.Text;
using Avalonia;
using ProtoInspector.Services;

namespace ProtoInspector;

internal static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        if (args.Length > 0 && string.Equals(args[0], "--smoke-test", StringComparison.OrdinalIgnoreCase))
        {
            return await SmokeTestRunner.RunAsync(args.Skip(1).ToArray());
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
