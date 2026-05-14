using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Verifies that ConfigurationService correctly maps every AppSettings property
/// to/from ConfigurationStore. Catches missing mappings when new settings are added.
/// </summary>
[TestFixture]
[NonParallelizable] // Modifies ConfigurationStore.Instance
public class ConfigurationServiceMappingTests
{
    private AppSettings _settings = null!;
    private ISettingsService _settingsService = null!;
    private ConfigurationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        // Reset ConfigurationStore to defaults before each test
        ConfigurationStore.SetInstance(new ConfigurationStore());

        _settings = new AppSettings();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Settings.Returns(_settings);

        _service = new ConfigurationService(
            Substitute.For<IVehicleProfileService>(),
            Substitute.For<IToolProfileService>(),
            _settingsService);
    }

    // =====================================================================
    // LoadAppSettings -> ConfigurationStore (AppSettings -> Store)
    // =====================================================================

    #region Display Settings: Load

    [TestCase(1920.0)]
    public void Load_WindowWidth(double value)
    {
        _settings.WindowWidth = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.WindowWidth, Is.EqualTo(value));
    }

    [TestCase(1080.0)]
    public void Load_WindowHeight(double value)
    {
        _settings.WindowHeight = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.WindowHeight, Is.EqualTo(value));
    }

    [TestCase(250.0)]
    public void Load_WindowX(double value)
    {
        _settings.WindowX = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.WindowX, Is.EqualTo(value));
    }

    [TestCase(150.0)]
    public void Load_WindowY(double value)
    {
        _settings.WindowY = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.WindowY, Is.EqualTo(value));
    }

    [Test]
    public void Load_WindowMaximized()
    {
        _settings.WindowMaximized = true;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.WindowMaximized, Is.True);
    }

    [Test]
    public void Load_StartFullscreen()
    {
        _settings.StartFullscreen = true;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.StartFullscreen, Is.True);
    }

    [Test]
    public void Load_SvennArrowVisible()
    {
        _settings.SvennArrowVisible = true;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.SvennArrowVisible, Is.True);
    }

    [Test]
    public void Load_KeyboardEnabled()
    {
        _settings.KeyboardEnabled = true;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.KeyboardEnabled, Is.True);
    }

    [TestCase(500.0)]
    public void Load_SimulatorPanelX(double value)
    {
        _settings.SimulatorPanelX = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.SimulatorPanelX, Is.EqualTo(value));
    }

    [TestCase(300.0)]
    public void Load_SimulatorPanelY(double value)
    {
        _settings.SimulatorPanelY = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.SimulatorPanelY, Is.EqualTo(value));
    }

    [Test]
    public void Load_SimulatorPanelVisible()
    {
        _settings.SimulatorPanelVisible = true;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.SimulatorPanelVisible, Is.True);
    }

    [Test]
    public void Load_GridVisible()
    {
        _settings.GridVisible = false;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.GridVisible, Is.False);
    }

    [Test]
    public void Load_CompassVisible()
    {
        _settings.CompassVisible = false;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.CompassVisible, Is.False);
    }

    [Test]
    public void Load_SpeedVisible()
    {
        _settings.SpeedVisible = false;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.SpeedVisible, Is.False);
    }

    [Test]
    public void Load_HeadlandDistanceVisible()
    {
        _settings.HeadlandDistanceVisible = false;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.HeadlandDistanceVisible, Is.False);
    }

    [TestCase(75.5)]
    public void Load_CameraZoom(double value)
    {
        _settings.CameraZoom = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.CameraZoom, Is.EqualTo(value));
    }

    [TestCase(-45.0)]
    public void Load_CameraPitch(double value)
    {
        _settings.CameraPitch = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Display.CameraPitch, Is.EqualTo(value));
    }

    #endregion

    #region Connection Settings: Load

    [Test]
    public void Load_NtripCasterHost()
    {
        _settings.NtripCasterIp = "rtk.example.com";
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.NtripCasterHost, Is.EqualTo("rtk.example.com"));
    }

    [TestCase(2102)]
    public void Load_NtripCasterPort(int value)
    {
        _settings.NtripCasterPort = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.NtripCasterPort, Is.EqualTo(value));
    }

    [Test]
    public void Load_NtripMountPoint()
    {
        _settings.NtripMountPoint = "RTCM3_Mount";
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.NtripMountPoint, Is.EqualTo("RTCM3_Mount"));
    }

    [Test]
    public void Load_NtripUsername()
    {
        _settings.NtripUsername = "user123";
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.NtripUsername, Is.EqualTo("user123"));
    }

    [Test]
    public void Load_NtripPassword()
    {
        _settings.NtripPassword = "secret";
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.NtripPassword, Is.EqualTo("secret"));
    }

    [Test]
    public void Load_NtripAutoConnect()
    {
        _settings.NtripAutoConnect = true;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.NtripAutoConnect, Is.True);
    }

    [Test]
    public void Load_AgShareServer()
    {
        _settings.AgShareServer = "https://custom.server";
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.AgShareServer, Is.EqualTo("https://custom.server"));
    }

    [Test]
    public void Load_AgShareApiKey()
    {
        _settings.AgShareApiKey = "api-key-123";
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.AgShareApiKey, Is.EqualTo("api-key-123"));
    }

    [Test]
    public void Load_AgShareEnabled()
    {
        _settings.AgShareEnabled = true;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.AgShareEnabled, Is.True);
    }

    [TestCase(20)]
    public void Load_GpsUpdateRate(int value)
    {
        _settings.GpsUpdateRate = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.GpsUpdateRate, Is.EqualTo(value));
    }

    [Test]
    public void Load_UseRtk()
    {
        _settings.UseRtk = false;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Connections.UseRtk, Is.False);
    }

    #endregion

    #region Simulator Settings: Load

    [Test]
    public void Load_SimulatorEnabled()
    {
        _settings.SimulatorEnabled = false;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Simulator.Enabled, Is.False);
    }

    [TestCase(51.5074)]
    public void Load_SimulatorLatitude(double value)
    {
        _settings.SimulatorLatitude = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Simulator.Latitude, Is.EqualTo(value));
    }

    [TestCase(-0.1278)]
    public void Load_SimulatorLongitude(double value)
    {
        _settings.SimulatorLongitude = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Simulator.Longitude, Is.EqualTo(value));
    }

    [TestCase(8.5)]
    public void Load_SimulatorSpeed(double value)
    {
        _settings.SimulatorSpeed = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Simulator.Speed, Is.EqualTo(value));
    }

    [TestCase(15.0)]
    public void Load_SimulatorSteerAngle(double value)
    {
        _settings.SimulatorSteerAngle = value;
        _service.LoadAppSettings();
        Assert.That(_service.Store.Simulator.SteerAngle, Is.EqualTo(value));
    }

    #endregion

    #region Hotkeys: Load

    [Test]
    public void Load_HotkeyBindings()
    {
        _settings.HotkeyBindings = new Dictionary<string, string>
        {
            { "AutoSteer", "Z" },
            { "CycleLines", "X" }
        };
        _service.LoadAppSettings();
        // Verify hotkeys were loaded (ToDictionary uses camelCase keys)
        var dict = _service.Store.Hotkeys.ToDictionary();
        Assert.That(dict, Does.ContainKey("autoSteer"));
        Assert.That(dict["autoSteer"], Is.EqualTo("Z"));
    }

    [Test]
    public void Load_EmptyHotkeyBindings_DoesNotCrash()
    {
        _settings.HotkeyBindings = new Dictionary<string, string>();
        Assert.DoesNotThrow(() => _service.LoadAppSettings());
    }

    #endregion

    // =====================================================================
    // SaveAppSettings -> AppSettings (Store -> AppSettings)
    // =====================================================================

    #region Display Settings: Save

    [Test]
    public void Save_DisplaySettings_RoundTrip()
    {
        // Set non-default values on store
        var store = _service.Store;
        store.Display.WindowWidth = 1920;
        store.Display.WindowHeight = 1080;
        store.Display.WindowX = 250;
        store.Display.WindowY = 150;
        store.Display.WindowMaximized = true;
        store.Display.StartFullscreen = true;
        store.Display.SvennArrowVisible = true;
        store.Display.KeyboardEnabled = true;
        store.Display.HeadlandDistanceVisible = false;
        store.Display.SimulatorPanelX = 500;
        store.Display.SimulatorPanelY = 300;
        store.Display.SimulatorPanelVisible = true;
        store.Display.GridVisible = false;
        store.Display.CompassVisible = false;
        store.Display.SpeedVisible = false;
        store.Display.CameraZoom = 75.5;
        store.Display.CameraPitch = -45.0;

        _service.SaveAppSettings();

        Assert.Multiple(() =>
        {
            Assert.That(_settings.WindowWidth, Is.EqualTo(1920));
            Assert.That(_settings.WindowHeight, Is.EqualTo(1080));
            Assert.That(_settings.WindowX, Is.EqualTo(250));
            Assert.That(_settings.WindowY, Is.EqualTo(150));
            Assert.That(_settings.WindowMaximized, Is.True);
            Assert.That(_settings.StartFullscreen, Is.True);
            Assert.That(_settings.SvennArrowVisible, Is.True);
            Assert.That(_settings.KeyboardEnabled, Is.True);
            Assert.That(_settings.HeadlandDistanceVisible, Is.False);
            Assert.That(_settings.SimulatorPanelX, Is.EqualTo(500));
            Assert.That(_settings.SimulatorPanelY, Is.EqualTo(300));
            Assert.That(_settings.SimulatorPanelVisible, Is.True);
            Assert.That(_settings.GridVisible, Is.False);
            Assert.That(_settings.CompassVisible, Is.False);
            Assert.That(_settings.SpeedVisible, Is.False);
            Assert.That(_settings.CameraZoom, Is.EqualTo(75.5));
            Assert.That(_settings.CameraPitch, Is.EqualTo(-45.0));
        });

        _settingsService.Received(1).Save();
    }

    #endregion

    #region Connection Settings: Save

    [Test]
    public void Save_ConnectionSettings_RoundTrip()
    {
        var store = _service.Store;
        store.Connections.NtripCasterHost = "rtk.example.com";
        store.Connections.NtripCasterPort = 2102;
        store.Connections.NtripMountPoint = "RTCM3";
        store.Connections.NtripUsername = "user";
        store.Connections.NtripPassword = "pass";
        store.Connections.NtripAutoConnect = true;
        store.Connections.AgShareServer = "https://custom";
        store.Connections.AgShareApiKey = "key-123";
        store.Connections.AgShareEnabled = true;
        store.Connections.GpsUpdateRate = 20;
        store.Connections.UseRtk = false;

        _service.SaveAppSettings();

        Assert.Multiple(() =>
        {
            Assert.That(_settings.NtripCasterIp, Is.EqualTo("rtk.example.com"));
            Assert.That(_settings.NtripCasterPort, Is.EqualTo(2102));
            Assert.That(_settings.NtripMountPoint, Is.EqualTo("RTCM3"));
            Assert.That(_settings.NtripUsername, Is.EqualTo("user"));
            Assert.That(_settings.NtripPassword, Is.EqualTo("pass"));
            Assert.That(_settings.NtripAutoConnect, Is.True);
            Assert.That(_settings.AgShareServer, Is.EqualTo("https://custom"));
            Assert.That(_settings.AgShareApiKey, Is.EqualTo("key-123"));
            Assert.That(_settings.AgShareEnabled, Is.True);
            Assert.That(_settings.GpsUpdateRate, Is.EqualTo(20));
            Assert.That(_settings.UseRtk, Is.False);
        });
    }

    #endregion

    #region Simulator Settings: Save

    [Test]
    public void Save_SimulatorSettings_RoundTrip()
    {
        var store = _service.Store;
        store.Simulator.Enabled = false;
        store.Simulator.Latitude = 51.5074;
        store.Simulator.Longitude = -0.1278;
        store.Simulator.Speed = 8.5;
        store.Simulator.SteerAngle = 15.0;

        _service.SaveAppSettings();

        Assert.Multiple(() =>
        {
            Assert.That(_settings.SimulatorEnabled, Is.False);
            Assert.That(_settings.SimulatorLatitude, Is.EqualTo(51.5074));
            Assert.That(_settings.SimulatorLongitude, Is.EqualTo(-0.1278));
            Assert.That(_settings.SimulatorSpeed, Is.EqualTo(8.5));
            Assert.That(_settings.SimulatorSteerAngle, Is.EqualTo(15.0));
        });
    }

    #endregion

    #region Hotkeys: Save

    [Test]
    public void Save_HotkeyBindings()
    {
        _service.Store.Hotkeys.LoadFromDictionary(new Dictionary<string, string>
        {
            { "AutoSteer", "Z" },
            { "CycleLines", "X" }
        });

        _service.SaveAppSettings();

        Assert.That(_settings.HotkeyBindings, Does.ContainKey("autoSteer"));
        Assert.That(_settings.HotkeyBindings["autoSteer"], Is.EqualTo("Z"));
    }

    #endregion

    #region ActiveProfile: Save

    [Test]
    public void Save_ActiveVehicleProfileName()
    {
        _service.Store.ActiveVehicleProfileName = "MyTractor";

        _service.SaveAppSettings();

        Assert.That(_settings.LastUsedVehicleProfile, Is.EqualTo("MyTractor"));
    }

    #endregion

    // =====================================================================
    // Full round-trip: Settings -> Store -> Settings
    // =====================================================================

    [Test]
    public void FullRoundTrip_AllMappedProperties_Preserved()
    {
        // Set non-default values on AppSettings
        _settings.WindowWidth = 1600;
        _settings.WindowHeight = 900;
        _settings.WindowX = 200;
        _settings.WindowY = 100;
        _settings.WindowMaximized = true;
        _settings.StartFullscreen = true;
        _settings.SvennArrowVisible = true;
        _settings.KeyboardEnabled = true;
        _settings.HeadlandDistanceVisible = false;
        _settings.GridVisible = false;
        _settings.CompassVisible = false;
        _settings.SpeedVisible = false;
        _settings.CameraZoom = 50.0;
        _settings.CameraPitch = -30.0;
        _settings.NtripCasterIp = "ntrip.test";
        _settings.NtripCasterPort = 9999;
        _settings.NtripMountPoint = "MP1";
        _settings.NtripUsername = "u";
        _settings.NtripPassword = "p";
        _settings.NtripAutoConnect = true;
        _settings.AgShareServer = "https://test";
        _settings.AgShareApiKey = "k";
        _settings.AgShareEnabled = true;
        _settings.GpsUpdateRate = 5;
        _settings.UseRtk = false;
        _settings.SimulatorEnabled = false;
        _settings.SimulatorLatitude = 48.8566;
        _settings.SimulatorLongitude = 2.3522;
        _settings.SimulatorSpeed = 12.0;
        _settings.SimulatorSteerAngle = -5.0;

        // Load into store
        _service.LoadAppSettings();

        // Save back to a fresh AppSettings
        var saved = new AppSettings();
        _settingsService.Settings.Returns(saved);
        _service.SaveAppSettings();

        // Verify all values survived the round-trip
        Assert.Multiple(() =>
        {
            Assert.That(saved.WindowWidth, Is.EqualTo(1600));
            Assert.That(saved.WindowHeight, Is.EqualTo(900));
            Assert.That(saved.WindowX, Is.EqualTo(200));
            Assert.That(saved.WindowY, Is.EqualTo(100));
            Assert.That(saved.WindowMaximized, Is.True);
            Assert.That(saved.StartFullscreen, Is.True);
            Assert.That(saved.SvennArrowVisible, Is.True);
            Assert.That(saved.KeyboardEnabled, Is.True);
            Assert.That(saved.HeadlandDistanceVisible, Is.False);
            Assert.That(saved.GridVisible, Is.False);
            Assert.That(saved.CompassVisible, Is.False);
            Assert.That(saved.SpeedVisible, Is.False);
            Assert.That(saved.CameraZoom, Is.EqualTo(50.0));
            Assert.That(saved.CameraPitch, Is.EqualTo(-30.0));
            Assert.That(saved.NtripCasterIp, Is.EqualTo("ntrip.test"));
            Assert.That(saved.NtripCasterPort, Is.EqualTo(9999));
            Assert.That(saved.NtripMountPoint, Is.EqualTo("MP1"));
            Assert.That(saved.NtripUsername, Is.EqualTo("u"));
            Assert.That(saved.NtripPassword, Is.EqualTo("p"));
            Assert.That(saved.NtripAutoConnect, Is.True);
            Assert.That(saved.AgShareServer, Is.EqualTo("https://test"));
            Assert.That(saved.AgShareApiKey, Is.EqualTo("k"));
            Assert.That(saved.AgShareEnabled, Is.True);
            Assert.That(saved.GpsUpdateRate, Is.EqualTo(5));
            Assert.That(saved.UseRtk, Is.False);
            Assert.That(saved.SimulatorEnabled, Is.False);
            Assert.That(saved.SimulatorLatitude, Is.EqualTo(48.8566));
            Assert.That(saved.SimulatorLongitude, Is.EqualTo(2.3522));
            Assert.That(saved.SimulatorSpeed, Is.EqualTo(12.0));
            Assert.That(saved.SimulatorSteerAngle, Is.EqualTo(-5.0));
        });
    }

    // =====================================================================
    // Completeness check: detect unmapped properties
    // =====================================================================

    [Test]
    public void Completeness_AllPersistableProperties_AreMapped()
    {
        // Properties that are intentionally NOT mapped through ConfigurationStore
        // (they are used directly from AppSettings or managed elsewhere)
        var excluded = new HashSet<string>
        {
            "FieldsDirectory",       // Used directly by FieldService
            "CurrentFieldName",      // Managed by FieldService
            "LastOpenedField",       // Managed by FieldService
            "IsFirstRun",           // Checked directly at startup
            "LastRunDate",          // Checked directly at startup
            "LastUsedVehicleProfile", // Mapped via ActiveVehicleProfileName (different name)
            "LastUsedToolProfile",    // Mapped via ActiveToolProfileName (different name) — #346
            "HotkeyBindings",       // Mapped via Hotkeys.LoadFromDictionary (special handling)
            "Language",              // Used directly from AppSettings for localization
            "HasMigratedIsMetric",   // One-shot migration latch — written by
                                     // ReconcileIsMetricAfterProfileLoad, not by
                                     // the Load/Save round-trip the rest of these
                                     // properties go through.
        };

        // Set every non-excluded property to a non-default value
        _settings.WindowWidth = 9999;
        _settings.WindowHeight = 9999;
        _settings.WindowX = 9999;
        _settings.WindowY = 9999;
        _settings.WindowMaximized = true;
        _settings.StartFullscreen = true;
        _settings.SvennArrowVisible = true;
        _settings.KeyboardEnabled = true;
        _settings.HeadlandDistanceVisible = false;
        _settings.ExtraGuidelines = true;
        _settings.ExtraGuidelinesCount = 5;
        _settings.FieldTextureVisible = true;
        _settings.AutoSteerSound = false;
        _settings.UTurnSound = false;
        _settings.HydraulicSound = false;
        _settings.SectionsSound = false;
        _settings.SimulatorPanelX = 9999;
        _settings.SimulatorPanelY = 9999;
        _settings.SimulatorPanelVisible = true;
        _settings.GridVisible = false;
        _settings.CompassVisible = false;
        _settings.SpeedVisible = false;
        _settings.ElevationLogEnabled = true;
        _settings.CameraZoom = 9999;
        _settings.CameraPitch = -50;
        _settings.CameraMode = CameraMode.HeadingUp; // default is Map; pick anything else

        _settings.DisplayResolutionMultiplier = 4.0;
        _settings.AutoDayNight = false;
        _settings.IsDayMode = false;
        _settings.NtripCasterIp = "MAPPED";
        _settings.NtripCasterPort = 9999;
        _settings.NtripMountPoint = "MAPPED";
        _settings.NtripUsername = "MAPPED";
        _settings.NtripPassword = "MAPPED";
        _settings.NtripAutoConnect = true;
        _settings.AgShareServer = "MAPPED";
        _settings.AgShareApiKey = "MAPPED";
        _settings.AgShareEnabled = true;
        _settings.GpsUpdateRate = 9999;
        _settings.UseRtk = false;
        _settings.SimulatorEnabled = false;
        _settings.SimulatorLatitude = 9999;
        _settings.SimulatorLongitude = 9999;
        _settings.SimulatorSpeed = 9999;
        _settings.SimulatorSteerAngle = 9999;
        _settings.FieldTextureVisible = false; // default is true, set to non-default
        _settings.FieldTextureMoveable = true; // default is false, set to non-default
        _settings.IsMetric = true; // default is false, set to non-default

        _service.LoadAppSettings();

        // Save to fresh settings and check nothing reverted to default
        var saved = new AppSettings();
        _settingsService.Settings.Returns(saved);
        _service.SaveAppSettings();

        // Check each mapped property got a non-default value back
        var props = typeof(AppSettings).GetProperties()
            .Where(p => !excluded.Contains(p.Name))
            .ToList();

        var defaults = new AppSettings();
        var unmapped = new List<string>();

        foreach (var prop in props)
        {
            var savedValue = prop.GetValue(saved);
            var defaultValue = prop.GetValue(defaults);

            if (Equals(savedValue, defaultValue))
            {
                unmapped.Add(prop.Name);
            }
        }

        Assert.That(unmapped, Is.Empty,
            $"These AppSettings properties appear unmapped in ConfigurationService: {string.Join(", ", unmapped)}");
    }

    // =====================================================================
    // IsMetric one-shot migration from legacy vehicle profile
    // =====================================================================

    [Test]
    public void ReconcileIsMetricAfterProfileLoad_FirstCall_MigratesProfileValueToAppSettings()
    {
        // Pre-migration state: AppSettings has the default (imperial), the
        // migration latch hasn't been flipped, and the profile load has
        // just written its legacy IsMetric=true into the store.
        _settings.IsMetric = false;
        _settings.HasMigratedIsMetric = false;
        _service.Store.IsMetric = true;

        _service.ReconcileIsMetricAfterProfileLoad();

        Assert.Multiple(() =>
        {
            Assert.That(_settings.IsMetric, Is.True,
                "First reconcile must migrate the profile value to AppSettings.");
            Assert.That(_settings.HasMigratedIsMetric, Is.True,
                "Migration latch must flip so subsequent profile loads don't repeat.");
            Assert.That(_service.Store.IsMetric, Is.True,
                "Store value remains the migrated value.");
        });
    }

    [Test]
    public void ReconcileIsMetricAfterProfileLoad_SubsequentCall_AppSettingsOverridesProfile()
    {
        // Post-migration state: AppSettings is authoritative and the user
        // has chosen metric. A profile load just wrote IsMetric=false into
        // the store (legacy field still present in the file). Reconcile
        // must put the store back on AppSettings' value.
        _settings.IsMetric = true;
        _settings.HasMigratedIsMetric = true;
        _service.Store.IsMetric = false;

        _service.ReconcileIsMetricAfterProfileLoad();

        Assert.Multiple(() =>
        {
            Assert.That(_service.Store.IsMetric, Is.True,
                "Post-migration, AppSettings overrides whatever the profile wrote.");
            Assert.That(_settings.IsMetric, Is.True,
                "AppSettings value is unchanged on subsequent reconciles.");
            Assert.That(_settings.HasMigratedIsMetric, Is.True,
                "Migration latch stays set.");
        });
    }
}
