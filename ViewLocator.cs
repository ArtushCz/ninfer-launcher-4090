using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using NinferLauncher_v_2.ViewModels;

namespace NinferLauncher_v_2;

/// <summary>
/// From a ViewModel finds the corresponding View (ViewModel → View, namespace Views).
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var baseName = param.GetType().Name.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType($"NinferLauncher_v_2.Views.{baseName}");

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + type?.Name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}