# Kleene Logic for C# (.NET)

> *Because sometimes the correct answer is not “yes” or “no”, but “we don’t know yet”.*

This repository contains a compact, fast implementation of **Kleene’s strong three-valued logic (K3)** for C# / .NET, with examples and tests.

## TL;DR

- `bool` is often too limited
- `bool?` has the right values but no operators
- **Kleene** or **three-valued** logic gives you `True`, `False`, and `Unknown` with real logic operators
- `Unknown` is designated uncertainty, not “false by accident”

If you’ve pulled a nullable Boolean (`bool?`) from a database and wished you could keep the uncertainty,
this library is for that.

---

## Why three-valued logic?

In many real systems, a yes/no answer is not always available.

Common examples:

- Database fields with `YES / NO / NULL`
- User input that hasn’t been provided yet
- External data that may be incomplete or delayed
- Business rules evaluated in stages
- Safety or validation checks with partial information

Using `bool` forces you to collapse:
- *unknown* → `false`
- or throw exceptions prematurely

Using `bool?` preserves the value — but gives you **no logic**.

**Kleene logic gives you both.**

---

## Nullable booleans, done right

This implementation maps cleanly to database semantics:

```csharp
true   -> Kleene.True
false  -> Kleene.False
null   -> Kleene.Unknown
```

And back:

```csharp
Kleene.Unknown -> null
```

Unlike `bool?`, you can now write:

```csharp
var canProceed = isApproved & hasPermission & !isSuspended;
```

Without accidentally treating `null` as `false`.

You can also provide explicit defaults when you need to collapse values:

```csharp
Kleene isAllowed = ruleResult.Default(Kleene.False);
bool canProceed = isApproved.Default(false);
```

Parsing from text is intentionally strict and case-insensitive:

```csharp
Kleene k = Kleene.Parse("unknown");
```

---

## Algebraic design

This implementation uses Kleene’s strong logic (K3) with explicit values:

| Value    | Meaning                         | Internal |
|---------|----------------------------------|----------|
| `True`  | Definitively true                | `+1` |
| `False` | Definitively false               | `-1` |
| `Unknown` | Not enough information yet     | `0` |

With the `{-1, 0, +1}` representation, the core operators are simple and fast:

| Operator | Definition |
|--------|------------|
| NOT | `!x = -x` |
| AND | `a & b = min(a, b)` |
| OR | `a \| b = max(a, b)` |
| XOR | `a ^ b = -(a * b)` (chosen semantics) |

This gives you:
- branch-free operators
- correct Kleene truth tables
- De Morgan’s laws
- excellent performance

---

## Control flow semantics (important!)

This implementation **does** define:

```csharp
operator true(Kleene)
operator false(Kleene)
```

with strict semantics:

true → only when value is True

false → only when value is False

Unknown → neither true nor false

Consequences:
```csharp
if (k)        // runs only if k == True
if (!k)       // runs only if k == False
```

If `k == Unknown`, neither executes.

This is deliberate.

Short-circuiting (`&&`, `||`)

`False && RHS()` short-circuits, and does not evaluate Right Hand Side `RHS()`.

`True || RHS()` short-circuits, and does not evaluate Right Hand Side `RHS()`.

`Unknown && RHS()` or `Unknown || RHS()` do not short-circuit: the RHS is evaluated.

This follows the philosophy:

`Unknown` does not control flow — only definitive knowledge does.

A word of caution about `else`:

```csharp
if (k)
{
    // runs only for True
}
else
{
    // runs for False OR Unknown
}
```

else means “not definitively true”, not “false”.

This also holds for:

```csharp
if (!k)
{
    // runs only for False
}
else
{
    // runs for True OR Unknown
}
```

For three-way logic, use an true/false operators:

```csharp
if (k) { ... }
else if (!k) { ... }
else { /* Unknown */ }
```

Or use the explicit properties:

```csharp
if (k.IsTrue) { ... }
else if (k.IsFalse) { ... }
else { /* Unknown */ }
```

## Example: HelloZoo 🦁🐘🐍

The repository includes an example: `HelloZoo.cs`

Animals have properties like:

- Is carnivore? (`Kleene`)
- Is tame? (`Kleene`)
- Number of legs (`int`)

The program:

- decides who gets meat
- decides who can be approached
- demonstrates `if`, `&&`, `||`
- shows how `Unknown` naturally propagates

It’s intentionally simple; the logic is the point.

---

## Testing

A complete xUnit test suite is included, covering:

- all operators
- all truth tables
- control-flow semantics
- short-circuit behavior (when the right hand side is evaluated)
- equality and ordering

Coverage is effectively **100% of code paths**.

---

## Migrating from `bool?` to `Kleene`

### Why migrate?

`bool?` has the *right values* but the *wrong behavior*:

- No logical operators
- `null` is easily lost
- Expressions become unreadable
- Control flow silently collapses uncertainty

```csharp
bool? a = ...;
bool? b = ...;

// What does this even mean?
if (a == true && b == true) { ... }
```

### Step 1: Replace types
- bool?
+ Kleene

### Step 2: Map database values explicitly

```csharp
Kleene FromDb(bool? value) => (Kleene)value;
bool? ToDb(Kleene value) => value.ToNullableBool();
```

Mappings are exact:

| DB |	Kleene |
| --- | --- |
| TRUE |	Kleene.True |
| FALSE |	Kleene.False |
| NULL |	Kleene.Unknown |

### Step 3: Replace boolean logic

```csharp
- if (a == true && b == true)
+ if (a & b)
```

Or, when control flow matters:

```csharp
if (a.IsTrue && b.IsTrue)
{
...
}
```

### Step 4: Make uncertainty explicit

Instead of silently ignoring null:

```csharp
if (k.IsUnknown)
{
Log("Decision deferred: insufficient data");
}
```

That’s what Kleene is intended for.

---

## JSON (System.Text.Json)

The included converter reads booleans, nulls, strings, and numeric tokens, and always writes a string:

```csharp
using KleeneLogic;
using KleeneLogic.Serialization;
using System.Text.Json;

var opts = new JsonSerializerOptions();
opts.Converters.Add(new KleeneJsonConverter());

var k = JsonSerializer.Deserialize<Kleene>("null", opts); // Unknown
```

---

## When to use it

This library doesn’t pretend three-valued logic is boolean logic.

Use Kleene logic when:

- `bool?` appears in your code
- `NULL` has real meaning
- you want to reason without throwing or guessing
- “unknown” is a first-class state

If everything is always known, `bool` is simpler and better.

---

## Release information

| Version | Notes                |
|---------|----------------------|
| 1.0.0   | Initial release      |
| 1.0.1   | Test release process |

