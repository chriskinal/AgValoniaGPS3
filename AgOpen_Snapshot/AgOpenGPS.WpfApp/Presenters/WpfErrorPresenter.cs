using AgOpenGPS.Core.Interfaces;
using System;

namespace AgOpenGPS.WpfApp.Presenters
{
    public class WpfErrorPresenter : IErrorPresenter
    {
        void IErrorPresenter.PresentTimedMessage(TimeSpan timeSpan, string titleString, string messageString)
        {
            throw new NotImplementedException();
        }
    }
}
