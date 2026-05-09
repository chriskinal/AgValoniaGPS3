// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial: handles the cycle worker's far-from-field warning.
/// The cycle detects when the live GPS position is well past any sane offset
/// from the loaded field's local-plane origin; this UI-thread handler drops
/// autosteer immediately and prompts the operator to close the field or keep
/// driving without guidance.
/// </summary>
public partial class MainViewModel
{
    private void HandleFarFromFieldWarning(FarFromFieldWarning warning)
    {
        if (IsAutoSteerEngaged)
        {
            IsAutoSteerEngaged = false;
            SyncGuidanceStateToPipeline();
        }

        double km = warning.DistanceMeters / 1000.0;
        ShowConfirmationDialog(
            "GPS far from field",
            $"GPS reports a position {km:F1} km from the loaded field origin. " +
            "Autosteer has been disabled.\n\n" +
            "Tap Yes to close the field, or No to keep driving without guidance.",
            () => _ = CloseFieldAsync());
    }
}
