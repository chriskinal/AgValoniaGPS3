using System;
using UIKit;

namespace AgValoniaGPS.iOS;

public class Application
{
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("[Main] Starting UIApplication.Main...");
            UIApplication.Main(args, null, typeof(AppDelegate));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Main] FATAL: {ex}");
            throw;
        }
    }
}
