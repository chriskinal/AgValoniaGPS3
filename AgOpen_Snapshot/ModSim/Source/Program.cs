using Microsoft.Win32;
using System;
using System.Threading;
using System.Windows.Forms;

namespace ModSim
{
    internal static class Program
    {
        private static readonly Mutex Mutex = new Mutex(true, "{8F6F0AC4-B9A1-66fd-A8CF-72F04E6BDE82}");

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            if (Mutex.WaitOne(TimeSpan.Zero, true))
            {

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FormSim());
            }
        }
    }
}
