using Курсовой_проект.Configuration;
using Xunit;

namespace FleetManagement.Tests;

public class DatabaseSettingsTests
{
    [Fact]
    public void PortableSettingsWorkWithoutUserFolderAndPreservePriority()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var app = Path.Combine(root, "app");
        var local = Path.Combine(root, "local");
        Directory.CreateDirectory(app);
        try
        {
            File.WriteAllText(Path.Combine(app, "connection.txt"), "  portable  \n");
            Assert.Equal("portable", DatabaseSettings.Resolve(null, app, local));
            Assert.False(Directory.Exists(local));
            Directory.CreateDirectory(Path.Combine(local, "FleetManagement"));
            File.WriteAllText(Path.Combine(local, "FleetManagement", "connection.txt"), "legacy");
            Assert.Equal("portable", DatabaseSettings.Resolve(null, app, local));
            Assert.Equal("environment", DatabaseSettings.Resolve("environment", app, local));
            File.Delete(Path.Combine(app, "connection.txt"));
            Assert.Equal("legacy", DatabaseSettings.Resolve(null, app, local));
            Directory.Delete(local, true);
            Assert.Throws<InvalidOperationException>(() => DatabaseSettings.Resolve(null, app, local));
        }
        finally { Directory.Delete(root, true); }
    }
}
