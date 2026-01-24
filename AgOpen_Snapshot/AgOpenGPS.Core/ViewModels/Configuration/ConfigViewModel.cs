namespace AgOpenGPS.Core.ViewModels
{
    public class ConfigViewModel : DayNightAndUnitsViewModel
    {
        private readonly ApplicationModel _appModel;

        public ConfigViewModel(ApplicationModel appModel)
        {
            _appModel = appModel;
        }

        public void UpdateFromSettings()
        {
            // Todo
        }

    }
}
