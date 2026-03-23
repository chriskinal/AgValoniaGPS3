using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Section;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests for SectionControlService - section on/off logic, manual overrides,
/// position calculations, and bitmask generation.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore is a singleton
public class SectionControlServiceTests
{
    private ICoverageMapService _coverageMap = null!;
    private IToolPositionService _toolPosition = null!;
    private ApplicationState _appState = null!;
    private SectionControlService _service = null!;

    [SetUp]
    public void SetUp()
    {
        // Isolate ConfigurationStore singleton
        ConfigurationStore.SetInstance(new ConfigurationStore());

        // Configure 3 sections, 200cm (2m) each = 6m total tool width
        var config = ConfigurationStore.Instance;
        config.NumSections = 3;
        config.Tool.SetSectionWidth(0, 200);
        config.Tool.SetSectionWidth(1, 200);
        config.Tool.SetSectionWidth(2, 200);
        config.Tool.Offset = 0;

        _coverageMap = Substitute.For<ICoverageMapService>();
        _toolPosition = Substitute.For<IToolPositionService>();
        _appState = new ApplicationState();

        _service = new SectionControlService(_toolPosition, _coverageMap, _appState);
    }

    #region Slow Speed

    [Test]
    public void Update_SlowSpeed_AllSectionsOff()
    {
        // Set all sections to Auto and manually turn them on first
        _service.SetAllAuto();
        _service.SetSectionState(0, SectionButtonState.On);
        _service.SetSectionState(1, SectionButtonState.On);

        // Update at very slow speed (below 0.5 m/s cutoff)
        _service.Update(new Vec3(50, 50, 0), 0, 0, 0.1);

        Assert.That(_service.SectionStates[0].IsOn, Is.False);
        Assert.That(_service.SectionStates[1].IsOn, Is.False);
        Assert.That(_service.SectionStates[2].IsOn, Is.False);
        Assert.That(_service.IsAnySectionOn, Is.False);
    }

    #endregion

    #region No Boundary

    [Test]
    public void Update_NoBoundary_SectionsStayOff()
    {
        // No boundary set on _appState.Field
        _service.SetAllAuto();
        _service.MasterState = SectionMasterState.Auto;

        _service.Update(new Vec3(50, 50, 0), 0, 0, 5.0);

        // Without a boundary, auto sections should stay off
        Assert.That(_service.IsAnySectionOn, Is.False);
    }

    #endregion

    #region Manual Override

    [Test]
    public void SetSectionState_ManualOn_TurnsOnImmediately()
    {
        _service.SetSectionState(0, SectionButtonState.On);

        Assert.That(_service.SectionStates[0].IsOn, Is.True);
        Assert.That(_service.SectionStates[0].ButtonState, Is.EqualTo(SectionButtonState.On));
    }

    [Test]
    public void SetSectionState_ManualOff_TurnsOffImmediately()
    {
        // First turn on
        _service.SetSectionState(1, SectionButtonState.On);
        Assert.That(_service.SectionStates[1].IsOn, Is.True);

        // Then force off
        _service.SetSectionState(1, SectionButtonState.Off);

        Assert.That(_service.SectionStates[1].IsOn, Is.False);
        Assert.That(_service.SectionStates[1].ButtonState, Is.EqualTo(SectionButtonState.Off));
    }

    [Test]
    public void SetAllAuto_ResetsAllSections()
    {
        _service.SetSectionState(0, SectionButtonState.On);
        _service.SetSectionState(1, SectionButtonState.Off);

        _service.SetAllAuto();

        Assert.That(_service.SectionStates[0].ButtonState, Is.EqualTo(SectionButtonState.Auto));
        Assert.That(_service.SectionStates[1].ButtonState, Is.EqualTo(SectionButtonState.Auto));
        Assert.That(_service.SectionStates[2].ButtonState, Is.EqualTo(SectionButtonState.Auto));
        Assert.That(_service.MasterState, Is.EqualTo(SectionMasterState.Auto));
    }

    #endregion

    #region TurnAllOff / MasterState

    [Test]
    public void TurnAllOff_SetsAllOff()
    {
        _service.SetSectionState(0, SectionButtonState.On);
        _service.SetSectionState(1, SectionButtonState.On);
        _service.SetSectionState(2, SectionButtonState.On);

        _service.TurnAllOff();

        Assert.That(_service.SectionStates[0].IsOn, Is.False);
        Assert.That(_service.SectionStates[1].IsOn, Is.False);
        Assert.That(_service.SectionStates[2].IsOn, Is.False);
        Assert.That(_service.IsAnySectionOn, Is.False);
    }

    [Test]
    public void MasterState_Off_TurnsAllOff()
    {
        // First set to Auto so we can transition to Off
        _service.MasterState = SectionMasterState.Auto;
        _service.SetSectionState(0, SectionButtonState.On);
        Assert.That(_service.SectionStates[0].IsOn, Is.True);

        _service.MasterState = SectionMasterState.Off;

        Assert.That(_service.SectionStates[0].IsOn, Is.False);
        Assert.That(_service.MasterState, Is.EqualTo(SectionMasterState.Off));
    }

    #endregion

    #region GetSectionBits

    [Test]
    public void GetSectionBits_ReturnsCorrectBitmask()
    {
        // Turn on sections 0 and 2 (bits 0b101 = 5)
        _service.SetSectionState(0, SectionButtonState.On);
        _service.SetSectionState(2, SectionButtonState.On);

        ushort bits = _service.GetSectionBits();

        Assert.That(bits, Is.EqualTo(0b101));
        Assert.That(bits & (1 << 0), Is.Not.Zero, "Section 0 bit should be set");
        Assert.That(bits & (1 << 1), Is.Zero, "Section 1 bit should not be set");
        Assert.That(bits & (1 << 2), Is.Not.Zero, "Section 2 bit should be set");
    }

    [Test]
    public void GetSectionBits_AllOff_ReturnsZero()
    {
        Assert.That(_service.GetSectionBits(), Is.EqualTo(0));
    }

    #endregion

    #region Section Positions

    [Test]
    public void GetSectionWorldPosition_ReturnsCorrectLeftRight()
    {
        // Tool at origin, heading north (0 radians)
        // 3 sections × 2m = 6m total, centered: [-3, -1, -1, 1, 1, 3]
        var toolPos = new Vec3(0, 0, 0);
        double heading = 0; // North

        var (left0, right0) = _service.GetSectionWorldPosition(0, toolPos, heading);
        var (left1, right1) = _service.GetSectionWorldPosition(1, toolPos, heading);
        var (left2, right2) = _service.GetSectionWorldPosition(2, toolPos, heading);

        // Section 0: left=-3m, right=-1m (to the left of center)
        Assert.That(left0.Easting, Is.EqualTo(-3.0).Within(0.01));
        Assert.That(right0.Easting, Is.EqualTo(-1.0).Within(0.01));

        // Section 1: left=-1m, right=1m (centered)
        Assert.That(left1.Easting, Is.EqualTo(-1.0).Within(0.01));
        Assert.That(right1.Easting, Is.EqualTo(1.0).Within(0.01));

        // Section 2: left=1m, right=3m (to the right of center)
        Assert.That(left2.Easting, Is.EqualTo(1.0).Within(0.01));
        Assert.That(right2.Easting, Is.EqualTo(3.0).Within(0.01));
    }

    [Test]
    public void RecalculateSectionPositions_UpdatesOnConfigChange()
    {
        // Change section width
        ConfigurationStore.Instance.Tool.SetSectionWidth(0, 400); // 4m

        // Recalculate
        _service.RecalculateSectionPositions();

        // Section 0 should now be 4m wide: total = 4+2+2 = 8m, centered at -4
        var (left0, right0) = _service.GetSectionWorldPosition(0, new Vec3(0, 0, 0), 0);
        Assert.That(right0.Easting - left0.Easting, Is.EqualTo(4.0).Within(0.01));
    }

    #endregion

    #region Event Firing

    [Test]
    public void SetSectionState_FiresSectionStateChangedEvent()
    {
        bool eventFired = false;
        _service.SectionStateChanged += (s, e) => eventFired = true;

        _service.SetSectionState(0, SectionButtonState.On);

        Assert.That(eventFired, Is.True);
    }

    #endregion
}
