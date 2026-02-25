using System.Windows;
using TaskTool.Services;

namespace TaskTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ServiceLocator.Initialize();
    }
}
