namespace LANPUT.Core;

public sealed record SettingsMigrationResult(string Status, string? ErrorType = null);

public static class SettingsMigration
{
    // Copy only the settings, preserving UDN/device name and leaving the legacy directory intact.
    public static SettingsMigrationResult CopyIfNeeded(string currentDirectory, string legacyDirectory)
    {
        var target = Path.Combine(currentDirectory, "settings.json");
        var legacy = Path.Combine(legacyDirectory, "settings.json");
        string? temporary = null;
        try
        {
            if (File.Exists(target)) return new("already-present");
            if (!File.Exists(legacy)) return new("no-legacy-settings");
            Directory.CreateDirectory(currentDirectory);
            temporary = Path.Combine(currentDirectory, "settings-migration-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.Copy(legacy, temporary, overwrite: false);
            try { File.Move(temporary, target, overwrite: false); }
            catch (IOException) when (File.Exists(target)) { return new("already-present"); }
            return new("copied");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { return new("failed", error.GetType().Name); }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
