using System.Windows;
using FamilyTree.UI.ViewModels;

namespace FamilyTree.UI.Views;

public sealed class ConfirmationService : IConfirmationService
{
    public bool Confirm(string title, string message) => MessageBox.Show(Application.Current.MainWindow,
        message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
}
