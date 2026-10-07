using System.Windows;
using FamilyTree.UI.ViewModels;

namespace FamilyTree.UI.Views;

public partial class FamilyTreeWindow : Window
{
    public FamilyTreeWindow(FamilyTreeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
