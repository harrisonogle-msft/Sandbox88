
using System.Diagnostics;

Entity[] entities = GenerateEntities();

Display(entities);

// Shuffle it.
Array.Sort(entities, static (Entity lhs, Entity rhs) =>
{
    return System.Diagnostics.Stopwatch.GetTimestamp() % 2 == 0 ? 1 : -1;
});

Display(entities);

IEnumerable<IGrouping<int, Entity>> groups = Wrap(entities).GroupBy(entity => entity.Group);
IGrouping<int, Entity>[] groupsArray = groups.ToArray();
Debugger.Break();

static IEnumerable<T> Wrap<T>(IEnumerable<T> items)
{
    ArgumentNullException.ThrowIfNull(items);

    foreach (T item in items)
    {
        yield return item;
    }
}

static Entity[] GenerateEntities()
{
    const char Begin = 'A';
    const char End = 'Z';

    int count = 1 + ((int)End) - ((int)Begin);

    var entities = new Entity[count];
    int i = 0;

    for (char ch = Begin; ch <= End; ++ch, ++i)
    {
        entities[i] = new Entity(i / 4, ch.ToString());
    }

    return entities;
}

static Entity[] GenerateEntities2()
{
    return new Entity[]
    {
        new(1, "A"),
        new(1, "B"),
        new(1, "C"),
        new(2, "D"),
        new(2, "E"),
        new(2, "F"),
        new(3, "G"),
        new(3, "H"),
        new(3, "I"),
    };
}

static void Display(IEnumerable<Entity> entities)
{
    Console.WriteLine("Entities:");
    foreach (Entity entity in entities)
    {
        Console.WriteLine($"  {entity?.ToString() ?? "null"}");
    }
}

record Entity(int Group, string Name);
