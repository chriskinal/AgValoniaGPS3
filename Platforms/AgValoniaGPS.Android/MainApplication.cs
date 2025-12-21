using Android.App;
using Android.Runtime;

namespace AgValoniaGPS.Android;

[Application]
public class MainApplication : Application
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }
}
