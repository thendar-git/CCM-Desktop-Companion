using System.Windows.Forms;

namespace CCM.DesktopCompanion;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var context = new CompanionApplicationContext();
        Application.Run(context);
    }
}
