using System;

namespace AgOpenGPS.Core.Models.Communication
{
    /// <summary>
    /// Event args for AutoSteer toggle requests
    /// </summary>
    public class AutoSteerToggleEventArgs : EventArgs
    {
    }

    /// <summary>
    /// Event args for section master button toggle requests
    /// </summary>
    public class SectionMasterToggleEventArgs : EventArgs
    {
        /// <summary>
        /// Which button to toggle: Auto or Manual
        /// </summary>
        public SectionMasterButton Button { get; set; }

        public enum SectionMasterButton
        {
            Auto,
            Manual
        }
    }
}
