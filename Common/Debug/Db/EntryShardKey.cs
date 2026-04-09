namespace Tyr.Common.Debug.Db;

internal readonly record struct EntryShardKey(
    Type Type,
    int ModuleId,
    int SourceLocationId,
    int ShardKeyId);
