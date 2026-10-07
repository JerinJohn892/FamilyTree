using System.Windows;
using FamilyTree.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyTree.UI.Views;

public sealed class TreeWindowService(IServiceProvider services) : ITreeWindowService
{
    private FamilyTreeWindow? window;
    public async Task ShowAsync()
    {
        if (window is null)
        {
            window = services.GetRequiredService<FamilyTreeWindow>();
            window.Owner = Application.Current.MainWindow;
            window.Closed += (_, _) => window = null;
            window.Show();
        }
        else
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
        }
        var vm = (FamilyTreeViewModel)window.DataContext;
        if (!vm.IsBusy) await vm.RefreshCommand.ExecuteAsync(null);
    }
}
