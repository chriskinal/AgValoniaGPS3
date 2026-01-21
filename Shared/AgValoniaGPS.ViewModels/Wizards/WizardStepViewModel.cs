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

using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.ViewModels.Wizards;

/// <summary>
/// Base class for wizard steps. Each step represents one page in a wizard.
/// </summary>
public abstract class WizardStepViewModel : ObservableObject
{
    /// <summary>
    /// The title displayed at the top of the step.
    /// </summary>
    public abstract string Title { get; }

    /// <summary>
    /// A brief description or instructions for this step.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Optional icon path for the step indicator.
    /// </summary>
    public virtual string? IconPath => null;

    /// <summary>
    /// Whether the user can proceed to the next step.
    /// Override to add validation logic.
    /// </summary>
    public virtual bool CanGoNext => true;

    /// <summary>
    /// Whether the user can go back to the previous step.
    /// Usually true unless this is a special step.
    /// </summary>
    public virtual bool CanGoBack => true;

    /// <summary>
    /// Whether this step can be skipped.
    /// </summary>
    public virtual bool CanSkip => false;

    /// <summary>
    /// Short label for step indicator (e.g., "1", "2", or icon name).
    /// </summary>
    public virtual string StepLabel => string.Empty;

    private string? _validationMessage;
    /// <summary>
    /// Validation error message to display, if any.
    /// </summary>
    public string? ValidationMessage
    {
        get => _validationMessage;
        protected set => SetProperty(ref _validationMessage, value);
    }

    /// <summary>
    /// Whether there is a validation error.
    /// </summary>
    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);

    private bool _isActive;
    /// <summary>
    /// Whether this step is currently visible/active.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        internal set
        {
            if (SetProperty(ref _isActive, value))
            {
                if (value)
                    OnEntering();
                else
                    OnLeaving();
            }
        }
    }

    /// <summary>
    /// Called when this step becomes active (visible).
    /// Override to initialize or refresh data.
    /// </summary>
    protected virtual void OnEntering() { }

    /// <summary>
    /// Called when leaving this step.
    /// Override to save intermediate state.
    /// </summary>
    protected virtual void OnLeaving() { }

    /// <summary>
    /// Validates the current step before allowing navigation to next.
    /// Override to add custom validation.
    /// </summary>
    /// <returns>True if valid, false if validation fails.</returns>
    public virtual Task<bool> ValidateAsync()
    {
        ValidationMessage = null;
        return Task.FromResult(true);
    }

    /// <summary>
    /// Clears any validation error message.
    /// </summary>
    protected void ClearValidation()
    {
        ValidationMessage = null;
        OnPropertyChanged(nameof(HasValidationError));
    }

    /// <summary>
    /// Sets a validation error message.
    /// </summary>
    protected void SetValidationError(string message)
    {
        ValidationMessage = message;
        OnPropertyChanged(nameof(HasValidationError));
    }
}
