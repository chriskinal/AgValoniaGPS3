using System.Linq;
using System.Reflection;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Verifies ConfigurationService maps configuration between AppSettings and
/// ConfigurationStore in both directions, and — crucially — that the mapping is
/// COMPLETE from the store's side. The historical bug was a guard that only
/// enumerated AppSettings, so a ConfigStore property with no AppSettings backing
/// silently never persisted. The completeness test here enumerates the STORE
/// sub-configs instead, so any new unmapped store property fails the build.
///
/// Persistent application STATE (window geometry, camera view, day/night value,
/// sim position, last field, boundary-recording setup) is NOT config and is
/// covered by PersistentStateServiceTests, not here.
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
        ConfigurationStore.SetInstance(new ConfigurationStore());

        _settings = new AppSettings();
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Settings.Returns(_settings);

        _service = new ConfigurationService(
            Substitute.For<IVehicleProfileService>(),
            Substitute.For<IToolProfileService>(),
            _settingsService);
    }

    // ── Representative round-trips ────────────────────────────────────────

    [Test]
    public void RoundTrip_DisplayPreferences_Preserved()
    {
        var d = _service.Store.Display;
        // Flip every boolean preference and set non-default numerics.
        d.GridVisible = false;
        d.CompassVisible = false;
        d.SpeedVisible = false;
        d.PolygonsVisible = false;
        d.SpeedometerVisible = false;
        d.KeyboardEnabled = true;
        d.HeadlandDistanceVisible = false;
        d.AutoDayNight = false;
        d.SvennArrowVisible = true;
        d.StartFullscreen = true;
        d.ElevationLogEnabled = true;
        d.FieldTextureVisible = false;
        d.FieldTextureMoveable = true;
        d.ExtraGuidelines = true;
        d.LineSmoothEnabled = false;
        d.DirectionMarkersVisible = true;
        d.SectionLinesVisible = false;
        d.UTurnButtonVisible = false;
        d.LateralButtonVisible = false;
        d.AutoSteerSound = false;
        d.UTurnSound = false;
        d.HydraulicSound = false;
        d.SectionsSound = false;
        d.HardwareMessagesEnabled = false;
        d.ExtraGuidelinesCount = 15;
        d.DayStartHour = 8;
        d.NightStartHour = 22;
        d.DisplayResolutionMultiplier = 2.5;
        d.DialogAutoCloseSeconds = 42;

        _service.SaveAppSettings();
        ConfigurationStore.SetInstance(new ConfigurationStore());
        _service.LoadAppSettings();

        var r = _service.Store.Display;
        Assert.Multiple(() =>
        {
            Assert.That(r.GridVisible, Is.False);
            Assert.That(r.CompassVisible, Is.False);
            Assert.That(r.SpeedVisible, Is.False);
            Assert.That(r.PolygonsVisible, Is.False);
            Assert.That(r.SpeedometerVisible, Is.False);
            Assert.That(r.KeyboardEnabled, Is.True);
            Assert.That(r.HeadlandDistanceVisible, Is.False);
            Assert.That(r.AutoDayNight, Is.False);
            Assert.That(r.SvennArrowVisible, Is.True);
            Assert.That(r.StartFullscreen, Is.True);
            Assert.That(r.ElevationLogEnabled, Is.True);
            Assert.That(r.FieldTextureVisible, Is.False);
            Assert.That(r.FieldTextureMoveable, Is.True);
            Assert.That(r.ExtraGuidelines, Is.True);
            Assert.That(r.LineSmoothEnabled, Is.False);
            Assert.That(r.DirectionMarkersVisible, Is.True);
            Assert.That(r.SectionLinesVisible, Is.False);
            Assert.That(r.UTurnButtonVisible, Is.False);
            Assert.That(r.LateralButtonVisible, Is.False);
            Assert.That(r.AutoSteerSound, Is.False);
            Assert.That(r.UTurnSound, Is.False);
            Assert.That(r.HydraulicSound, Is.False);
            Assert.That(r.SectionsSound, Is.False);
            Assert.That(r.HardwareMessagesEnabled, Is.False);
            Assert.That(r.ExtraGuidelinesCount, Is.EqualTo(15));
            Assert.That(r.DayStartHour, Is.EqualTo(8));
            Assert.That(r.NightStartHour, Is.EqualTo(22));
            Assert.That(r.DisplayResolutionMultiplier, Is.EqualTo(2.5));
            Assert.That(r.DialogAutoCloseSeconds, Is.EqualTo(42));
        });
    }

    [Test]
    public void RoundTrip_Connections_Preserved()
    {
        var c = _service.Store.Connections;
        c.NtripCasterHost = "rtk.example.com";
        c.NtripCasterPort = 2102;
        c.NtripMountPoint = "RTCM3";
        c.NtripUsername = "user";
        c.NtripPassword = "pass";
        c.NtripAutoConnect = true;
        c.AgShareServer = "https://custom";
        c.AgShareApiKey = "key-123";
        c.AgShareEnabled = true;
        c.GpsUpdateRate = 20;
        c.UseRtk = false;

        _service.SaveAppSettings();
        ConfigurationStore.SetInstance(new ConfigurationStore());
        _service.LoadAppSettings();

        var r = _service.Store.Connections;
        Assert.Multiple(() =>
        {
            Assert.That(r.NtripCasterHost, Is.EqualTo("rtk.example.com"));
            Assert.That(r.NtripCasterPort, Is.EqualTo(2102));
            Assert.That(r.NtripMountPoint, Is.EqualTo("RTCM3"));
            Assert.That(r.NtripUsername, Is.EqualTo("user"));
            Assert.That(r.NtripPassword, Is.EqualTo("pass"));
            Assert.That(r.NtripAutoConnect, Is.True);
            Assert.That(r.AgShareServer, Is.EqualTo("https://custom"));
            Assert.That(r.AgShareApiKey, Is.EqualTo("key-123"));
            Assert.That(r.AgShareEnabled, Is.True);
            Assert.That(r.GpsUpdateRate, Is.EqualTo(20));
            Assert.That(r.UseRtk, Is.False);
        });
    }

    [Test]
    public void RoundTrip_SimulatorEnabledPreference_Preserved()
    {
        _service.Store.Simulator.Enabled = false;
        _service.SaveAppSettings();
        ConfigurationStore.SetInstance(new ConfigurationStore());
        _service.LoadAppSettings();
        Assert.That(_service.Store.Simulator.Enabled, Is.False);
    }

    [Test]
    public void RoundTrip_Hotkeys_Preserved()
    {
        _service.Store.Hotkeys.LoadFromDictionary(new System.Collections.Generic.Dictionary<string, string>
        {
            { "AutoSteer", "Z" },
            { "CycleLines", "X" }
        });
        _service.SaveAppSettings();
        Assert.That(_settings.HotkeyBindings, Does.ContainKey("autoSteer"));
        Assert.That(_settings.HotkeyBindings["autoSteer"], Is.EqualTo("Z"));
    }

    [Test]
    public void Save_ActiveVehicleProfileName_MapsToLastUsed()
    {
        _service.Store.ActiveVehicleProfileName = "MyTractor";
        _service.SaveAppSettings();
        Assert.That(_settings.LastUsedVehicleProfile, Is.EqualTo("MyTractor"));
    }

    // ── Completeness guard (store-driven, bidirectional) ──────────────────

    /// <summary>
    /// Every persistable DisplayConfig property must round-trip through
    /// AppSettings, OR be explicitly listed as persisted-elsewhere / transient.
    /// Adding a DisplayConfig property without giving it a home fails here —
    /// the regression that let 12 display toggles silently never persist.
    /// </summary>
    [Test]
    public void Completeness_EveryDisplayConfigProperty_IsMappedOrAccountedFor()
    {
        // Display properties intentionally NOT persisted via AppSettings.
        // DialogAutoCloseEnabled is a derived bool view of DialogAutoCloseSeconds
        // (which IS persisted) — the Screen & Alerts toggle binds to it; it has
        // no independent state to store.
        var notPersistedViaAppSettings = new System.Collections.Generic.HashSet<string>
        {
            nameof(DisplayConfig.DialogAutoCloseEnabled),
        };

        var displayProps = typeof(DisplayConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(p => p.Name)
            .ToList();

        var settingsProps = typeof(AppSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        var unmapped = displayProps
            .Where(name => !notPersistedViaAppSettings.Contains(name))
            .Where(name => !settingsProps.Contains(name))
            .ToList();

        Assert.That(unmapped, Is.Empty,
            "DisplayConfig properties with no AppSettings backing (map them in " +
            "ConfigurationService + AppSettings, or add to the allowlist if they " +
            $"persist elsewhere / are transient): {string.Join(", ", unmapped)}");
    }

    /// <summary>
    /// SimulatorConfig must not regrow persistable position fields — those are
    /// state (PersistentAppState). Only the Enabled preference is config; Heading
    /// is transient runtime.
    /// </summary>
    [Test]
    public void Completeness_SimulatorConfig_OnlyEnabledIsConfig()
    {
        var transient = new System.Collections.Generic.HashSet<string> { "Heading" };

        var props = typeof(SimulatorConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(p => p.Name)
            .ToList();

        var settingsProps = typeof(AppSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        // AppSettings names the simulator config with a "Simulator" prefix
        // (SimulatorConfig.Enabled -> AppSettings.SimulatorEnabled).
        var unaccounted = props
            .Where(name => !transient.Contains(name))
            .Where(name => !settingsProps.Contains("Simulator" + name))
            .ToList();

        Assert.That(unaccounted, Is.Empty,
            "SimulatorConfig has persistable fields with no config home. Position " +
            "fields belong in PersistentAppState, not here: " + string.Join(", ", unaccounted));
    }

    // ── IsMetric one-shot migration ───────────────────────────────────────

    [Test]
    public void ReconcileIsMetricAfterProfileLoad_FirstCall_MigratesProfileValueToAppSettings()
    {
        _settings.IsMetric = false;
        _settings.HasMigratedIsMetric = false;
        _service.Store.IsMetric = true;

        _service.ReconcileIsMetricAfterProfileLoad();

        Assert.Multiple(() =>
        {
            Assert.That(_settings.IsMetric, Is.True);
            Assert.That(_settings.HasMigratedIsMetric, Is.True);
            Assert.That(_service.Store.IsMetric, Is.True);
        });
    }

    [Test]
    public void ReconcileIsMetricAfterProfileLoad_SubsequentCall_AppSettingsOverridesProfile()
    {
        _settings.IsMetric = true;
        _settings.HasMigratedIsMetric = true;
        _service.Store.IsMetric = false;

        _service.ReconcileIsMetricAfterProfileLoad();

        Assert.Multiple(() =>
        {
            Assert.That(_service.Store.IsMetric, Is.True);
            Assert.That(_settings.IsMetric, Is.True);
            Assert.That(_settings.HasMigratedIsMetric, Is.True);
        });
    }
}
