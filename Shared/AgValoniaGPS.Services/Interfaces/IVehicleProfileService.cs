using AgValoniaGPS.Models;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for managing vehicle profiles (tractor + implement configurations)
/// Compatible with AgOpenGPS vehicle XML format
/// </summary>
public interface IVehicleProfileService
{
    /// <summary>
    /// Gets the directory where vehicle profiles are stored
    /// </summary>
    string VehiclesDirectory { get; }

    /// <summary>
    /// Gets the currently active vehicle profile
    /// </summary>
    VehicleProfile? ActiveProfile { get; }

    /// <summary>
    /// Gets a list of available vehicle profile names (filenames without .XML extension)
    /// </summary>
    /// <returns>List of profile names</returns>
    List<string> GetAvailableProfiles();

    /// <summary>
    /// Loads a vehicle profile by name
    /// </summary>
    /// <param name="profileName">Profile name (filename without .XML extension)</param>
    /// <returns>The loaded vehicle profile, or null if not found</returns>
    VehicleProfile? Load(string profileName);

    /// <summary>
    /// Saves a vehicle profile to disk
    /// </summary>
    /// <param name="profile">The profile to save</param>
    void Save(VehicleProfile profile);

    /// <summary>
    /// Sets the active profile by name
    /// </summary>
    /// <param name="profileName">Profile name to activate</param>
    /// <returns>True if profile was loaded and activated successfully</returns>
    bool SetActiveProfile(string profileName);

    /// <summary>
    /// Creates a new default profile with the given name
    /// </summary>
    /// <param name="profileName">Name for the new profile</param>
    /// <returns>The newly created profile</returns>
    VehicleProfile CreateDefaultProfile(string profileName);

    /// <summary>
    /// Event fired when the active profile changes
    /// </summary>
    event EventHandler<VehicleProfile?>? ActiveProfileChanged;
}
