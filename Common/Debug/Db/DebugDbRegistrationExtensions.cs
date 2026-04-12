namespace Tyr.Common.Debug.Db;

public static class DebugDbRegistrationExtensions
{
    public static DebugDb RegisterKnownTypes(this DebugDb db)
    {
        ArgumentNullException.ThrowIfNull(db);

        foreach (var type in DebugTypeRegistry.GetRegisteredTypes())
            db.RegisterType(type);

        return db;
    }
}
