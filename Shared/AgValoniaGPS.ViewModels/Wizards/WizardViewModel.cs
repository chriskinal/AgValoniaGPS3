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
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels.Wizards;

/// <summary>
/// Base ViewModel for wizards. Manages step navigation and progress tracking.
/// </summary>
public abstract class WizardViewModel : ObservableObject
{
    /// <summary>
    /// All steps in this wizard.
    /// </summary>
    public ObservableCollection<WizardStepViewModel> Steps { get; } = new();

    private WizardStepViewModel? _currentStep;
    /// <summary>
    /// The currently active step.
    /// </summary>
    public WizardStepViewModel? CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (_currentStep != value)
            {
                if (_currentStep != null)
                    _currentStep.IsActive = false;

                SetProperty(ref _currentStep, value);

                if (_currentStep != null)
                    _currentStep.IsActive = true;

                UpdateNavigationState();
            }
        }
    }

    private int _currentStepIndex;
    /// <summary>
    /// Zero-based index of the current step.
    /// </summary>
    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        private set
        {
            if (SetProperty(ref _currentStepIndex, value))
            {
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(ProgressPercent));
                OnPropertyChanged(nameof(StepDisplay));
            }
        }
    }

    /// <summary>
    /// Total number of steps in the wizard.
    /// </summary>
    public int TotalSteps => Steps.Count;

    /// <summary>
    /// Progress as a fraction (0.0 to 1.0).
    /// </summary>
    public double Progress => TotalSteps > 0 ? (CurrentStepIndex + 1.0) / TotalSteps : 0;

    /// <summary>
    /// Progress as a percentage (0 to 100).
    /// </summary>
    public int ProgressPercent => (int)(Progress * 100);

    /// <summary>
    /// Display string for step indicator (e.g., "Step 3 of 10").
    /// </summary>
    public string StepDisplay => $"Step {CurrentStepIndex + 1} of {TotalSteps}";

    /// <summary>
    /// Title of the wizard (displayed in header).
    /// </summary>
    public abstract string WizardTitle { get; }

    private bool _isDialogVisible;
    /// <summary>
    /// Whether the wizard dialog is visible.
    /// </summary>
    public bool IsDialogVisible
    {
        get => _isDialogVisible;
        set => SetProperty(ref _isDialogVisible, value);
    }

    private bool _canGoNext;
    /// <summary>
    /// Whether the Next button should be enabled.
    /// </summary>
    public bool CanGoNext
    {
        get => _canGoNext;
        private set => SetProperty(ref _canGoNext, value);
    }

    private bool _canGoBack;
    /// <summary>
    /// Whether the Back button should be enabled.
    /// </summary>
    public bool CanGoBack
    {
        get => _canGoBack;
        private set => SetProperty(ref _canGoBack, value);
    }

    private bool _canSkip;
    /// <summary>
    /// Whether the Skip button should be visible.
    /// </summary>
    public bool CanSkip
    {
        get => _canSkip;
        private set => SetProperty(ref _canSkip, value);
    }

    private bool _isOnLastStep;
    /// <summary>
    /// Whether we're on the last step (shows Finish instead of Next).
    /// </summary>
    public bool IsOnLastStep
    {
        get => _isOnLastStep;
        private set => SetProperty(ref _isOnLastStep, value);
    }

    private bool _isOnFirstStep;
    /// <summary>
    /// Whether we're on the first step.
    /// </summary>
    public bool IsOnFirstStep
    {
        get => _isOnFirstStep;
        private set => SetProperty(ref _isOnFirstStep, value);
    }

    /// <summary>
    /// Command to go to the next step.
    /// </summary>
    public ICommand NextCommand { get; }

    /// <summary>
    /// Command to go to the previous step.
    /// </summary>
    public ICommand BackCommand { get; }

    /// <summary>
    /// Command to skip the current step.
    /// </summary>
    public ICommand SkipCommand { get; }

    /// <summary>
    /// Command to cancel the wizard.
    /// </summary>
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Command to finish the wizard (on last step).
    /// </summary>
    public ICommand FinishCommand { get; }

    /// <summary>
    /// Event raised when the wizard is completed successfully.
    /// </summary>
    public event EventHandler? Completed;

    /// <summary>
    /// Event raised when the wizard is cancelled.
    /// </summary>
    public event EventHandler? Cancelled;

    /// <summary>
    /// Event raised when requesting to close the wizard dialog.
    /// </summary>
    public event EventHandler? CloseRequested;

    protected WizardViewModel()
    {
        NextCommand = new AsyncRelayCommand(GoNextAsync, () => CanGoNext);
        BackCommand = new RelayCommand(GoBack, () => CanGoBack);
        SkipCommand = new RelayCommand(Skip, () => CanSkip);
        CancelCommand = new RelayCommand(Cancel);
        FinishCommand = new AsyncRelayCommand(FinishAsync, () => IsOnLastStep);
    }

    /// <summary>
    /// Initialize the wizard with its steps.
    /// Call this after adding all steps.
    /// </summary>
    protected void Initialize()
    {
        if (Steps.Count > 0)
        {
            CurrentStepIndex = 0;
            CurrentStep = Steps[0];
        }
        UpdateNavigationState();
    }

    /// <summary>
    /// Go to the next step.
    /// </summary>
    private async Task GoNextAsync()
    {
        if (CurrentStep == null || CurrentStepIndex >= Steps.Count - 1)
            return;

        // Validate current step
        var isValid = await CurrentStep.ValidateAsync();
        if (!isValid)
            return;

        CurrentStepIndex++;
        CurrentStep = Steps[CurrentStepIndex];
    }

    /// <summary>
    /// Go to the previous step.
    /// </summary>
    private void GoBack()
    {
        if (CurrentStepIndex <= 0)
            return;

        CurrentStepIndex--;
        CurrentStep = Steps[CurrentStepIndex];
    }

    /// <summary>
    /// Skip the current step.
    /// </summary>
    private void Skip()
    {
        if (CurrentStep?.CanSkip != true || CurrentStepIndex >= Steps.Count - 1)
            return;

        CurrentStepIndex++;
        CurrentStep = Steps[CurrentStepIndex];
    }

    /// <summary>
    /// Cancel the wizard.
    /// </summary>
    private void Cancel()
    {
        OnCancelled();
        Cancelled?.Invoke(this, EventArgs.Empty);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Finish the wizard.
    /// </summary>
    private async Task FinishAsync()
    {
        if (CurrentStep == null)
            return;

        // Validate final step
        var isValid = await CurrentStep.ValidateAsync();
        if (!isValid)
            return;

        // Allow derived classes to do final processing
        await OnCompletingAsync();

        Completed?.Invoke(this, EventArgs.Empty);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Navigate to a specific step by index.
    /// </summary>
    public void GoToStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= Steps.Count)
            return;

        CurrentStepIndex = stepIndex;
        CurrentStep = Steps[stepIndex];
    }

    /// <summary>
    /// Updates navigation button states.
    /// </summary>
    private void UpdateNavigationState()
    {
        IsOnFirstStep = CurrentStepIndex == 0;
        IsOnLastStep = CurrentStepIndex == Steps.Count - 1;
        CanGoBack = !IsOnFirstStep && (CurrentStep?.CanGoBack ?? true);
        CanGoNext = !IsOnLastStep && (CurrentStep?.CanGoNext ?? true);
        CanSkip = CurrentStep?.CanSkip ?? false;

        // Notify commands to re-evaluate CanExecute
        (NextCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (BackCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SkipCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (FinishCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Override to perform actions when wizard is cancelled.
    /// </summary>
    protected virtual void OnCancelled() { }

    /// <summary>
    /// Override to perform final processing before wizard completes.
    /// </summary>
    protected virtual Task OnCompletingAsync() => Task.CompletedTask;

    /// <summary>
    /// Add a step to the wizard.
    /// </summary>
    protected void AddStep(WizardStepViewModel step)
    {
        Steps.Add(step);
        OnPropertyChanged(nameof(TotalSteps));
    }
}
