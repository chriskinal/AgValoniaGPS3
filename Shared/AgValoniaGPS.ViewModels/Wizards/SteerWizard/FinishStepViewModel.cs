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

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Final step - summary and completion.
/// </summary>
public class FinishStepViewModel : WizardStepViewModel
{
    public override string Title => "Setup Complete";

    public override string Description =>
        "Your basic AutoSteer configuration is complete!\n\n" +
        "Click Finish to save your settings. You can always modify these values " +
        "later in the Configuration panel.\n\n" +
        "For advanced steering calibration (WAS, CPD, Ackermann), run this wizard again " +
        "or use the Tools menu.";

    public override bool CanSkip => false;
}
