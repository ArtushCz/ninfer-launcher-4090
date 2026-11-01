using System;
using Avalonia.Controls;
using NinferLauncher_v_2.ViewModels;

namespace NinferLauncher_v_2.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as MainViewModel)?.OnWindowClosing();
        base.OnClosed(e);
    }
}