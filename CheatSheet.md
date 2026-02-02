## Cheat sheet

### Truth values

| Kleene | Meaning |
|------|--------|
| `True` | Definitively true |
| `False` | Definitively false |
| `Unknown` | Not known / not resolved |

---

### Core operators

#### NOT (`!`)

| x | !x |
|---|----|
| True | False |
| False | True |
| Unknown | Unknown |

---

#### AND (`&`)

| a \ b | False | Unknown | True |
|------|-------|---------|------|
| False | False | False | False |
| Unknown | False | Unknown | Unknown |
| True | False | Unknown | True |

---

#### OR (`|`)

| a \ b | False | Unknown | True |
|------|-------|---------|------|
| False | False | Unknown | True |
| Unknown | Unknown | Unknown | True |
| True | True | True | True |

---

#### XOR (`^`) – chosen semantics

| a \ b | False | Unknown | True |
|------|-------|---------|------|
| False | False | Unknown | True |
| Unknown | Unknown | Unknown | Unknown |
| True | True | Unknown | False |

---

### Control flow rules

#### `if (k)`

- Executes **only** if `k == True`
- Skips for `False` **and** `Unknown`

#### `if (!k)`

- Executes **only** if `k == False`
- Skips for `True` **and** `Unknown`

#### `else`

- Means: **“not definitively true”**
- Includes both `False` **and** `Unknown`

---

### Short-circuiting

| Expression    | Right Hand Side evaluated? |
|---------------|----------------------------|
| `False && RHS()` | ❌ No                       |
| `True  && RHS()` | ✅ Yes                      |
| `Unknown && RHS()` | ✅ Yes                      |
| `True \|\| RHS()` | ❌ No                       |
| `False \|\| RHS()` | ✅ Yes                      |
| `Unknown \|\| RHS()` | ✅ Yes                      |

**Rule:**
> Only fully resolved values (`True` / `False`) control flow.

---

### Recommended explicit branching

```csharp
if (k.IsTrue) { ... }
else if (k.IsFalse) { ... }
else { /* Unknown */ }
```
