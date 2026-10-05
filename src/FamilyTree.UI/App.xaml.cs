using System.IO;
using System.Windows;
using FamilyTree.Core.Abstractions;
using FamilyTree.Core.Services;
using FamilyTree.Core.Validation;
using FamilyTree.Data;
using FamilyTree.UI.ViewModels;
using FamilyTree.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyTree.UI;

public partial class App : Application
{
    private ServiceProvider? services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var path = Environment.GetEnvironmentVariable("FAMILYTREE_DB_PATH");
            if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyTree", "familytree.db");
            var database = new SqliteDatabase(path);
            await Task.Run(database.Initialize);
            var registrations = new ServiceCollection();
            registrations.AddSingleton<IUnitOfWorkFactory>(database);
            registrations.AddSingleton(TimeProvider.System);
            registrations.AddSingleton<MemberValidator>();
            registrations.AddSingleton<RelationshipValidator>();
            registrations.AddSingleton<TreeIntegrityValidator>();
            registrations.AddSingleton<IRelationshipResolver, RelationshipResolver>();
            registrations.AddSingleton<IFamilyTreeTraversalService, FamilyTreeTraversalService>();
            registrations.AddTransient<IFamilyMemberService, FamilyMemberService>();
            registrations.AddTransient<IRelationshipService, RelationshipService>();
            registrations.AddSingleton<IConfirmationService, ConfirmationService>();
            registrations.AddTransient<MemberEditorViewModel>();
            registrations.AddTransient<RelationshipEditorViewModel>();
            registrations.AddTransient<MainViewModel>();
            registrations.AddTransient<MainWindow>();
            services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            var window = services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
            await ((MainViewModel)window.DataContext).InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"FamilyTree could not open its database. Check that the database folder is writable.\n\n{ex.Message}",
                "Startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        services?.Dispose();
        base.OnExit(e);
    }
}
