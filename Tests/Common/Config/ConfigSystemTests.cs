using Tomlet;
using Tomlet.Models;
using Tyr.Common.Config;

namespace Tyr.Tests.Common.Config;

[Configurable("Sample configurable used by the config system tests")]
public sealed partial class SampleConfig
{
    [ConfigEntry("A float with a default that is not exactly representable in binary")]
    public static partial float Gain { get; set; } = 1.2f;

    [ConfigEntry(StorageType.User)] public static partial int Count { get; set; } = 3;
    [ConfigEntry] public static partial float? Optional { get; set; }
    [ConfigEntry(editable: false)] public static partial string Name { get; set; } = "sample";
}

[Configurable]
public sealed partial class LateConfig
{
    [ConfigEntry] public static partial int Value { get; set; } = 1;
}

/// <summary>
/// These tests mutate the process-global <see cref="Registry"/> and the static entries of
/// <see cref="SampleConfig"/>/<see cref="LateConfig"/>, and one of them attaches a real
/// <see cref="Storage"/> to the registry. Anything else touching the config system must therefore
/// not run at the same time, so every such test class joins this collection.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConfigSystemCollection
{
    public const string Name = "Config system";
}

[Collection(ConfigSystemCollection.Name)]
public class ConfigSystemTests
{
    private static Configurable Sample
    {
        get
        {
            var configurable = SampleConfig.Configurable;
            Registry.Register(configurable);
            configurable.SetDefaults();
            return configurable;
        }
    }

    [Fact]
    public void Entries_DescribeTheGeneratedProperties()
    {
        var configurable = Sample;

        Assert.Equal(typeof(SampleConfig), configurable.Type);
        Assert.Equal("Sample configurable used by the config system tests", configurable.Description);
        Assert.Equal(["Gain", "Count", "Optional", "Name"], configurable.Entries.Select(e => e.Name));

        var gain = Assert.IsType<ConfigEntry<float>>(configurable.Find("Gain"));
        Assert.Equal(1.2f, gain.Default);
        Assert.Equal(StorageType.Project, gain.StorageType);
        Assert.True(gain.Editable);
        Assert.NotNull(gain.Description);
        Assert.Same(configurable, gain.Owner);

        Assert.Equal(StorageType.User, configurable.Find("Count")!.StorageType);
        Assert.Equal(typeof(float?), configurable.Find("Optional")!.Type);
        Assert.False(configurable.Find("Name")!.Editable);
    }

    [Fact]
    public void Setter_NotifiesOnlyWhenTheValueChanges()
    {
        var configurable = Sample;
        var events = new List<StorageType>();
        configurable.OnUpdated += events.Add;
        var version = configurable.Version;
        var registryVersion = Registry.Version;

        try
        {
            SampleConfig.Gain = 1.2f; // same as default
            Assert.Empty(events);
            Assert.Equal(version, configurable.Version);

            SampleConfig.Gain = 2f;
            Assert.Equal([StorageType.Project], events);
            Assert.Equal(version + 1, configurable.Version);
            Assert.Equal(registryVersion + 1, Registry.Version);

            SampleConfig.Count = 4;
            Assert.Equal([StorageType.Project, StorageType.User], events);
        }
        finally
        {
            configurable.OnUpdated -= events.Add;
        }
    }

    [Fact]
    public void EntryValue_RoundTripsThroughTheProperty()
    {
        var configurable = Sample;
        var entry = configurable.Find("Optional")!;

        Assert.Null(entry.Value);
        entry.Value = 7f;
        Assert.Equal(7f, SampleConfig.Optional);
        Assert.Equal(7f, entry.Value);

        entry.ResetToDefault();
        Assert.Null(SampleConfig.Optional);
    }

    [Fact]
    public void Set_AlwaysNotifiesExactlyOnce()
    {
        var configurable = Sample;
        var count = 0;
        void Handler(StorageType _) => count++;
        configurable.OnUpdated += Handler;

        try
        {
            configurable.Find("Name")!.Set("sample"); // unchanged value
            Assert.Equal(1, count);
        }
        finally
        {
            configurable.OnUpdated -= Handler;
        }
    }

    [Fact]
    public void Batch_CoalescesNotificationsPerStorageType()
    {
        var configurable = Sample;
        var events = new List<StorageType>();
        void Handler(StorageType storageType) => events.Add(storageType);
        configurable.OnUpdated += Handler;

        try
        {
            using (configurable.BeginBatch())
            {
                SampleConfig.Gain = 5f;
                SampleConfig.Optional = 1f;
                SampleConfig.Count = 9;
                Assert.Empty(events);
            }

            Assert.Equal(2, events.Count);
            Assert.Contains(StorageType.Project, events);
            Assert.Contains(StorageType.User, events);
        }
        finally
        {
            configurable.OnUpdated -= Handler;
        }
    }

    [Fact]
    public void SuppressNotifications_AppliesValuesWithoutNotifying()
    {
        var configurable = Sample;
        var events = new List<StorageType>();
        void Handler(StorageType storageType) => events.Add(storageType);
        configurable.OnUpdated += Handler;

        try
        {
            var version = configurable.Version;
            var registryVersion = Registry.Version;

            using (configurable.SuppressNotifications())
            {
                SampleConfig.Gain = 7f; // through the generated setter
                configurable.Find("Name")!.Set("suppressed"); // through ConfigEntry.Set, which always marks
            }

            // the values are applied ...
            Assert.Equal(7f, SampleConfig.Gain);
            Assert.Equal("suppressed", SampleConfig.Name);

            // ... but nothing was notified, so an attached Storage never learns of them
            Assert.Empty(events);
            Assert.Equal(version, configurable.Version);
            Assert.Equal(registryVersion, Registry.Version);

            // and the scope really did end
            SampleConfig.Gain = 8f;
            Assert.Equal([StorageType.Project], events);
            Assert.Equal(version + 1, configurable.Version);
        }
        finally
        {
            configurable.OnUpdated -= Handler;
        }
    }

    [Fact]
    public void FromToml_AppliesValuesAndNotifiesOnce()
    {
        var configurable = Sample;
        var count = 0;
        void Handler(StorageType _) => count++;
        configurable.OnUpdated += Handler;

        try
        {
            var table = new TomlTable();
            table.PutValue("Gain", new TomlDouble(4.5));
            table.PutValue("Optional", new TomlDouble(0.25));
            table.PutValue("Name", new TomlString("loaded"));
            table.PutValue("Count", new TomlLong(42)); // user entry, must be ignored for a project load

            configurable.FromToml(table, StorageType.Project);

            Assert.Equal(1, count);
            Assert.Equal(4.5f, SampleConfig.Gain);
            Assert.Equal(0.25f, SampleConfig.Optional);
            Assert.Equal("loaded", SampleConfig.Name);
            Assert.Equal(3, SampleConfig.Count);
        }
        finally
        {
            configurable.OnUpdated -= Handler;
        }
    }

    [Fact]
    public void ToToml_WritesFloatsWithoutBinaryConversionNoise()
    {
        var configurable = Sample;

        var table = configurable.ToToml(StorageType.Project);

        Assert.Equal(1.2, Assert.IsType<TomlDouble>(table.GetValue("Gain")).Value);
        Assert.False(table.ContainsKey("Optional")); // null values are not written
        Assert.False(table.ContainsKey("Count")); // user entry

        var document = TomlDocument.CreateEmpty();
        Registry.WriteToml(document, StorageType.Project);
        Assert.Contains("Gain = 1.2 #", document.SerializedValue);
    }

    [Fact]
    public void TomlPath_DropsTheRootNamespaceSegment()
    {
        Assert.Equal("Tests.Common.Config.SampleConfig", Registry.TomlPath(Sample));
    }

    [Fact]
    public void Tree_ContainsRegisteredConfigurablesAsLeaves()
    {
        var configurable = Sample;

        var node = Registry.Tree.Children["Tests"].Children["Common"].Children["Config"].Children["SampleConfig"];
        Assert.Same(configurable, node.Configurable);
        Assert.True(node.IsLeaf);
        Assert.False(Registry.Tree.Children["Tests"].IsLeaf);
    }

    [Fact]
    public void Storage_LoadsValuesReplaysOntoLateRegistrationsAndPreservesForeignTables()
    {
        var directory = Directory.CreateTempSubdirectory("tyr-config-test");
        var path = Path.Combine(directory.FullName, "config.toml");

        try
        {
            File.WriteAllText(path, """
                [Tests.Common.Config.SampleConfig]
                Gain = 9.5
                Name = "from file"

                [Tests.Common.Config.LateConfig]
                Value = 77

                [Some.Other.Module]
                Untouched = true
                """);

            _ = Sample;
            LateConfig.Value = 1;

            using (var storage = new Storage(path, StorageType.Project))
            {
                Assert.Equal(9.5f, SampleConfig.Gain);
                Assert.Equal("from file", SampleConfig.Name);
                Assert.Equal(1, LateConfig.Value); // not registered yet

                Registry.Register(LateConfig.Configurable);
                Assert.Equal(77, LateConfig.Value); // replayed from the loaded document

                SampleConfig.Gain = 2.5f;
            }

            var document = TomlParser.ParseFile(path);
            Assert.Equal(2.5f, ((TomlTable)document.GetValue("Tests.Common.Config.SampleConfig")).GetFloat("Gain"));
            Assert.Equal(77, ((TomlTable)document.GetValue("Tests.Common.Config.LateConfig")).GetLong("Value"));
            Assert.True(((TomlTable)document.GetValue("Some.Other.Module")).GetBoolean("Untouched"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
