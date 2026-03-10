using System.Reflection;

namespace Tyr.Soccer.Plays;

public sealed class PlayBook
{
    public sealed class Entry
    {
        private readonly MethodInfo _isApplicable;

        public Type Type { get; }
        public string Name { get; }

        internal Entry(Type type)
        {
            Type = type;
            Name = type.Name;
            _isApplicable = type.GetMethod(nameof(IPlay.IsApplicable))!;
        }

        public bool IsApplicable() => (bool)_isApplicable.Invoke(null, null)!;
        public IPlay Instantiate() => (IPlay)Activator.CreateInstance(Type)!;
    }

    public IReadOnlyList<Entry> Plays { get; }

    public PlayBook()
    {
        Plays = typeof(IPlay).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IPlay)))
            .Select(t => new Entry(t))
            .ToList();
    }

    /// <summary>
    /// Returns an instance of the first applicable play, or null if none applies.
    /// Plays are checked in discovery order; callers should order <see cref="Plays"/> by priority if needed.
    /// </summary>
    public IPlay? FindApplicable()
    {
        foreach (var entry in Plays)
        {
            if (entry.IsApplicable())
                return entry.Instantiate();
        }

        return null;
    }
}
