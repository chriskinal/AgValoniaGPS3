namespace AgOpenGPS.Core.Models.Communication
{
    /// <summary>
    /// Current UI state needed for module communication switch logic.
    /// Passed to Core service to determine what actions should be taken.
    /// </summary>
    public class ModuleSwitchState
    {
        /// <summary>
        /// Is AutoSteer currently enabled in UI?
        /// </summary>
        public bool IsAutoSteerOn { get; set; }

        /// <summary>
        /// Is AutoSteer in "Auto" mode (remote control)?
        /// </summary>
        public bool IsAutoSteerAuto { get; set; }

        /// <summary>
        /// Current state of Auto section button (Off, Auto, On)
        /// </summary>
        public int AutoButtonState { get; set; }

        /// <summary>
        /// Current state of Manual section button (Off, On)
        /// </summary>
        public int ManualButtonState { get; set; }
    }

    /// <summary>
    /// Button states enum (matches WinForms btnStates)
    /// </summary>
    public static class ButtonStates
    {
        public const int Off = 0;
        public const int Auto = 1;
        public const int On = 2;
    }
}
