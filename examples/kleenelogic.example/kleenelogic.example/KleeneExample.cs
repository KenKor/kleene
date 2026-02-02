#nullable enable
using KleeneLogic;

namespace KleeneLogic.Example;

/// <summary>
/// HelloZoo
/// </summary>
public static class KleeneExample
{
    private sealed record Animal(
        string Name,
        string Species,
        Kleene Carnivore,
        Kleene Tame,
        int Legs
    );

    public static void Run()
    {
        Console.WriteLine("=== Example: HelloZoo 🦁🐘🐍 ===");
        Console.WriteLine();
        
        var animals = new List<Animal>
        {
            //  Name                        Species                Carnivore             Tame                 Legs
            new("Simba",      "Lion",       Kleene.True,    Kleene.False,   4),
            new("Dumbo",      "Elephant",   Kleene.False,   Kleene.Unknown, 4),
            new("Whiskers",   "Cat",        Kleene.True,    Kleene.True,    4),
            new("Rex",        "Dog",        Kleene.Unknown, Kleene.True,    4),
            new("George",     "Monkey",     Kleene.Unknown, Kleene.Unknown, 2),
            new("Porky",      "Pig",        Kleene.Unknown, Kleene.True,    4),
            new("Henrietta",  "Chicken",    Kleene.False,   Kleene.True,    2),
            new("Thumper",    "Rabbit",     Kleene.False,   Kleene.True,    4),
            new("Peanut",     "Guinea pig", Kleene.False,   Kleene.True,    4),
            new("Kaa",        "Snake",      Kleene.True,    Kleene.False,   0),

            // A few illustrative extras
            new("Sparkle",    "Goldfish",   Kleene.False,   Kleene.Unknown, 0),
            new("Spike",      "T-Rex",      Kleene.True,    Kleene.False,   2),
            new("Marty",      "Alien",      Kleene.Unknown, Kleene.Unknown, 3),
        };

        PrintRoster(animals);

        Console.WriteLine();
        Console.WriteLine("=== Feeding plan: who gets meat? ===");
        foreach (var a in animals)
        {
            // Demonstrates operator true: only definite True enters the 'if'
            if (a.Carnivore)
            {
                Console.WriteLine($"Feed meat to: {a.Name} the {a.Species}");
                continue;
            }
            // Carnivore is not definitively true. Could be False or Unknown.
            else if (!a.Carnivore)
            {
                Console.WriteLine($"No meat:      {a.Name} the {a.Species} (herbivore/omnivore)");
            }
            else // (a.Carnivore.IsUnknown)
            {
                Console.WriteLine($"Unsure diet:  {a.Name} the {a.Species} (carnivore unknown) -> offer mixed food");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Safety plan: who can we approach? ===");
        foreach (var a in animals)
        {
            // Simple rule of thumb for demo:
            // - Approach if definitely tame
            // - Do not approach if definitely NOT tame
            // - If unknown, use extra checks
            if (a.Tame)
            {
                Console.WriteLine($"Approach:     {a.Name} the {a.Species}");
                continue;
            }

            if (!a.Tame)
            {
                Console.WriteLine($"Do NOT approach: {a.Name} the {a.Species} (not tame)");
                continue;
            }

            // At this point Tame must be Unknown.
            // We'll do a couple illustrative rules to decide what to do.
            //
            // This is intentionally explicit: Unknown is neither true nor false, so it falls through.
            var likelyDangerous =
                (a.Carnivore && HasBiteRisk(a)) // shows && with Kleene
                | (a.Species is "Snake" or "Lion" or "T-Rex" ? Kleene.True : Kleene.False);

            if (likelyDangerous)
            {
                Console.WriteLine($"Do NOT approach: {a.Name} the {a.Species} (tameness unknown, risk high)");
            }
            else if (likelyDangerous.IsUnknown)
            {
                Console.WriteLine($"Approach carefully: {a.Name} the {a.Species} (tameness unknown, risk unknown)");
            }
            else
            {
                Console.WriteLine($"Approach cautiously: {a.Name} the {a.Species} (tameness unknown, risk low)");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Illustrations / observations ===");
        IllustrateTriStateIfBehavior();
        IllustrateUnknownRhsEvaluation();
    }

    private static void PrintRoster(List<Animal> animals)
    {
        Console.WriteLine("=== Zoo roster ===");
        foreach (var a in animals)
        {
            Console.WriteLine($"{a.Name,-10} | {a.Species,-10} | Carnivore: {a.Carnivore,-7} | Tame: {a.Tame,-7} | Legs: {a.Legs}");
        }
    }

    /// <summary>
    /// Domain-ish check that returns Kleene to demonstrate composition.
    /// For demo purposes: animals with zero legs or weird leg counts get Unknown bite risk.
    /// </summary>
    private static Kleene HasBiteRisk(Animal a)
    {
        // Many "real" checks in unit systems look like this: some facts are known, some aren't.
        // We return Kleene rather than forcing a bool.
        if (a.Species is "Rabbit" or "Guinea pig" or "Chicken" or "Pig" or "Elephant" or "Goldfish")
            return Kleene.False;

        if (a.Species is "Lion" or "Snake" or "T-Rex" or "Cat" or "Dog")
            return Kleene.True;

        // Unknown species / odd physiology -> unknown risk
        if (a.Legs is 0 or < 0 or > 8)
            return Kleene.Unknown;

        return Kleene.Unknown;
    }

    private static void IllustrateTriStateIfBehavior()
    {
        Console.WriteLine("1) if (Unknown) and if (!Unknown) both do not execute:");

        var hit = 0;

        if (Kleene.Unknown) hit++;
        if (!Kleene.Unknown) hit++;

        Console.WriteLine(hit == 0
            ? "   OK: Unknown is neither true nor false in control flow."
            : $"   Unexpected: hit={hit}");
    }

    private static void IllustrateUnknownRhsEvaluation()
    {
        Console.WriteLine("2) Unknown evaluates RHS for && and || (by design):");

        var calls = 0;

        Kleene Rhs(string label)
        {
            calls++;
            Console.WriteLine($"   RHS evaluated: {label}");
            return Kleene.True;
        }

        _ = Kleene.Unknown && Rhs("Unknown && RHS");
        _ = Kleene.Unknown || Rhs("Unknown || RHS");

        Console.WriteLine($"   RHS call count = {calls} (expected: 2)");
    }
}
