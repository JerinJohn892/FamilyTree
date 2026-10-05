using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FamilyTree.UI.ViewModels;

namespace FamilyTree.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnDateValidationError(object? sender, DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException = false;
        ((MainViewModel)DataContext).MemberEditor.DateInputError = "Enter a valid birth date or leave it blank.";
    }

    private void OnBirthDateLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Input parsing belongs to the presentation boundary; future-date rules remain in Core.
        var editor = ((MainViewModel)DataContext).MemberEditor;
        var text = BirthDatePicker.Text;
        editor.DateInputError = string.IsNullOrWhiteSpace(text) || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out _)
            ? "" : "Enter a valid birth date or leave it blank.";
    }
}
