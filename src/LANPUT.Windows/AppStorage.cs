using System.IO;
using LANPUT.Core;

namespace LANPUT.Windows;

internal static class AppStorage
{
    public static string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LANPUT");
    public static string? MigrationNotice { get; private set; }
    public static void MigrateLegacySettings()
    {
        var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LanCast");
        var result = SettingsMigration.CopyIfNeeded(DataDirectory, legacy);
        MigrationNotice = result.Status switch
        {
            "copied" => "已迁移旧版设置：保留设备身份、名称与网卡，旧目录未改动。",
            "failed" => "旧版设置迁移失败 · " + result.ErrorType + "，请在接收设置中检查设备名称和网卡。",
            _ => null
        };
    }
}
