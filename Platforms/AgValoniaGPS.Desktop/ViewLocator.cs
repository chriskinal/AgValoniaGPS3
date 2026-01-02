using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Desktop;

public class ViewLocator : IDataTemplate
{
    // Cache the Views assembly for faster lookups
    private static readonly Assembly ViewsAssembly = typeof(AgValoniaGPS.Views.Controls.DrawingContextMapControl).Assembly;

    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var vmFullName = param.GetType().FullName!;
        string viewName;

        // Handle wizard step ViewModels - they live in Controls.Wizards in Views project
        if (vmFullName.Contains(".ViewModels.Wizards."))
        {
            // AgValoniaGPS.ViewModels.Wizards.SteerWizard.WelcomeStepViewModel
            // -> AgValoniaGPS.Views.Controls.Wizards.SteerWizard.WelcomeStepView
            viewName = vmFullName
                .Replace(".ViewModels.Wizards.", ".Views.Controls.Wizards.")
                .Replace("ViewModel", "View", StringComparison.Ordinal);
        }
        else
        {
            // Default behavior: replace ViewModel with View
            viewName = vmFullName.Replace("ViewModel", "View", StringComparison.Ordinal);
        }

        // Try to find type in Views assembly first (where most views live)
        var type = ViewsAssembly.GetType(viewName);

        // Fall back to Type.GetType for types in other assemblies
        if (type == null)
        {
            type = Type.GetType(viewName);
        }

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + viewName };
    }

    public bool Match(object? data)
    {
        return data is ObservableObject;
    }
}
