// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

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
