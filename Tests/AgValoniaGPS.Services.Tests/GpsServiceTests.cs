using AgValoniaGPS.Models;
using AgValoniaGPS.Services;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class GpsServiceTests
{
    private GpsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new GpsService();
    }

    [Test]
    public void IsGpsDataOk_NoDataReceived_ReturnsFalse()
    {
        Assert.That(_service.IsGpsDataOk(), Is.False);
    }

    [Test]
    public void IsGpsDataOk_AfterValidUpdate_ReturnsTrue()
    {
        _service.UpdateGpsData(new GpsData { IsValid = true });
        Assert.That(_service.IsGpsDataOk(), Is.True);
    }

    [Test]
    public void IsConnected_AfterValidUpdate_IsTrue()
    {
        _service.UpdateGpsData(new GpsData { IsValid = true });
        Assert.That(_service.IsConnected, Is.True);
    }

    [Test]
    public void IsConnected_AfterInvalidUpdate_IsFalse()
    {
        _service.UpdateGpsData(new GpsData { IsValid = false });
        Assert.That(_service.IsConnected, Is.False);
    }

    [Test]
    public void IsGpsDataOk_SetsDisconnectedOnTimeout()
    {
        _service.UpdateGpsData(new GpsData { IsValid = true });
        Assert.That(_service.IsConnected, Is.True);

        System.Threading.Thread.Sleep(400);

        bool ok = _service.IsGpsDataOk();
        Assert.That(ok, Is.False);
        Assert.That(_service.IsConnected, Is.False);
    }

    [Test]
    public void IsConnected_RecoverAfterNewData()
    {
        _service.UpdateGpsData(new GpsData { IsValid = true });
        System.Threading.Thread.Sleep(400);
        _service.IsGpsDataOk();
        Assert.That(_service.IsConnected, Is.False);

        _service.UpdateGpsData(new GpsData { IsValid = true });
        Assert.That(_service.IsConnected, Is.True);
        Assert.That(_service.IsGpsDataOk(), Is.True);
    }

    [Test]
    public void Start_SetsConnected()
    {
        _service.Start();
        Assert.That(_service.IsConnected, Is.True);
    }

    [Test]
    public void Stop_ClearsConnected()
    {
        _service.Start();
        _service.Stop();
        Assert.That(_service.IsConnected, Is.False);
    }
}
