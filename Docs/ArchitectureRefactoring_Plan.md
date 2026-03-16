# 🏗️ LivingSim Architecture Refactoring Plan

## Overview

This plan details the refactoring needed to transition from your current single-threaded architecture to a **layered simulation stack architecture**. This prevents code tangles and enables scaling to 1000+ entities without performance collapse.

**Current Problem:** WorldManager is a god object. Systems update independently. No orchestration layer.

**Expected Result:** Clean separation of concerns with synchronized simulation ticks.

---

## Phase A — Extract Simulation Tick Manager (PRIORITY 1)

**Goal:** Create the heartbeat of the simulation. Everything updates synchronously on this timer.

### A1: Create `SimulationTickManager.cs`

```csharp
public partial class SimulationTickManager : Node
{
    [Export] public float TickRate = 0.016f; // 60 ticks/sec
    private float tickTimer = 0f;
    
    private List<Animal> allAnimals = new();
    private List<EnvironmentObject> allEnvironment = new();
    
    public override void _Process(double delta)
    {
        tickTimer += (float)delta;
        
        if (tickTimer >= TickRate)
        {
            tickTimer = 0f;
            ExecuteSimulationTick();
        }
    }
    
    private void ExecuteSimulationTick()
    {
        // Order matters! These phases execute synchronously:
        
        // Phase 1: Update environment (plants, regeneration)
        foreach (var env in allEnvironment)
            env.SimulationTick();
        
        // Phase 2: Update animals (behavior, movement, interaction)
        foreach (var animal in allAnimals)
            animal.SimulationTick();
        
        // Phase 3: Resolve interactions (predation, eating)
        ResolveInteractions();
        
        // Phase 4: Population management
        RemoveDeadAnimals();
        
        // Phase 5: Event dispatching
        DispatchEvents();
    }
}
```

### A2: Modify `Animal.cs`

Remove `_Process()` entirely. Add `SimulationTick()`:

```csharp
public void SimulationTick()
{
    // Existing _Process logic goes here
    // But called synchronously from SimulationTickManager
}
```

### A3: Modify `BerryBush.cs`

Remove `_Process()` entirely. Add `SimulationTick()`:

```csharp
public void SimulationTick()
{
    // Existing _Process logic for regrowth
}
```

**Why This Matters:**
- Animals update in deterministic order
- No race conditions
- Can serialize/replay the simulation
- Testing becomes possible

---

## Phase B — Extract Season Manager (PRIORITY 1)

**Goal:** Decouple season/weather logic from WorldManager.

### B1: Create `SeasonManager.cs`

```csharp
public partial class SeasonManager : Node
{
    public static SeasonType CurrentSeason { get; private set; } = SeasonType.Spring;
    
    [Export] public float SeasonDuration = 60f;
    private float seasonTimer = 0f;
    
    [Signal] public delegate void SeasonChangedEventHandler(SeasonType newSeason);
    
    public override void _Process(double delta)
    {
        seasonTimer += (float)delta;
        if (seasonTimer >= SeasonDuration)
        {
            seasonTimer = 0f;
            AdvanceSeason();
        }
    }
    
    private void AdvanceSeason()
    {
        CurrentSeason = (SeasonType)(((int)CurrentSeason + 1) % 4);
        EmitSignal(SignalName.SeasonChanged, CurrentSeason);
    }
}
```

### B2: Remove from `WorldManager.cs`

Delete the season timer logic and static `CurrentSeason`.

**Why This Matters:**
- seasons update independently from world management
- UI can subscribe to season changes without polling
- Multiple managers can listen to seasons

---

## Phase C — Decouple Plant ↔ Animal Interaction (PRIORITY 2)

**Goal:** Remove direct coupling where animals directly call `BerryBush.EatBerry()`.

### C1: Create `InteractionManager.cs`

```csharp
public partial class InteractionManager : Node
{
    [Signal] public delegate void PlantConsumedEventHandler(Node3D plant, Animal consumer);
    
    // Register food sources with the manager
    private Dictionary<HexTile, List<Node3D>> registeredFood = new();
    
    public void RegisterFoodSource(HexTile tile, Node3D plant)
    {
        if (!registeredFood.ContainsKey(tile))
            registeredFood[tile] = new();
        registeredFood[tile].Add(plant);
    }
    
    public void UnregisterFoodSource(HexTile tile, Node3D plant)
    {
        if (registeredFood.ContainsKey(tile))
            registeredFood[tile].Remove(plant);
    }
    
    // Animals request food consumption through the manager
    public bool ConsumeFood(Animal animal, Node3D foodSource)
    {
        if (foodSource is BerryBush bush)
        {
            if (bush.HasFood())
            {
                bush.EatBerry();
                EmitSignal(SignalName.PlantConsumed, foodSource, animal);
                return true;
            }
        }
        return false;
    }
}
```

### C2: Modify `Animal.cs`

Replace direct `bush.EatBerry()` with:

```csharp
// OLD (BAD):
// if (targetFood is BerryBush bush) bush.EatBerry();

// NEW (GOOD):
if (InteractionManager.Singleton.ConsumeFood(this, targetFood))
{
    // Success, hunger replenished
}
```

**Why This Matters:**
- Animals don't know about BerryBush internals
- Easy to add new food types (just implement in InteractionManager)
- UI can listen to PlantConsumed events for stats

---

## Phase D — Reorganize Folder Structure (PRIORITY 2)

### Current Structure → New Structure

```
Scripts/
├── Simulation/          (rename to Systems/)
│   ├── SimulationTickManager.cs  [NEW]
│   ├── SeasonManager.cs          [NEW]
│   ├── PopulationManager.cs       [NEW]
│   ├── InteractionManager.cs      [NEW]
│   └── Animal.cs (keep here for now)
│
├── World/              (keep as-is)
│   ├── WorldManager.cs
│   ├── HexTile.cs
│   ├── BiomeType.cs
│   └── VegetationSpawner.cs
│
├── Environment/        [NEW - RENAME from Utility]
│   ├── BerryBush.cs
│   ├── Tree.cs         [future]
│   └── Rock.cs         [future]
│
├── Observer/           [NEW]
│   ├── CameraController.cs  (move from Utility)
│   ├── AnimalInspector.cs   [NEW]
│   ├── PopulationGraph.cs   [NEW]
│   └── TimeControls.cs      [NEW]
│
└── Utility/            (empty after moves)
```

**Commands to execute:**
```powershell
# Rename Simulation → Systems
mv Scripts/Simulation Scripts/Systems

# Create Environment folder and move files
mkdir Scripts/Environment
mv Scripts/Utility/BerryBush.cs Scripts/Environment/

# Create Observer folder and move files
mkdir Scripts/Observer
mv Scripts/Utility/CameraController.cs Scripts/Observer/
```

---

## Phase E — Create Population Manager (PRIORITY 3)

**Goal:** Track species populations and statistics. Prevent extinction/explosion.

### E1: Create `PopulationManager.cs`

```csharp
public partial class PopulationManager : Node
{
    private Dictionary<Type, List<Animal>> speciesRegistry = new();
    
    [Signal] public delegate void AnimalBornEventHandler(Animal newborn);
    [Signal] public delegate void AnimalDiedEventHandler(Animal deceased, string cause);
    
    public void RegisterAnimal(Animal animal)
    {
        var type = animal.GetType();
        if (!speciesRegistry.ContainsKey(type))
            speciesRegistry[type] = new();
        speciesRegistry[type].Add(animal);
        EmitSignal(SignalName.AnimalBorn, animal);
    }
    
    public void UnregisterAnimal(Animal animal, string deathCause)
    {
        var type = animal.GetType();
        if (speciesRegistry.ContainsKey(type))
            speciesRegistry[type].Remove(animal);
        EmitSignal(SignalName.AnimalDied, animal, deathCause);
    }
    
    public Dictionary<string, int> GetPopulationStats()
    {
        var stats = new Dictionary<string, int>();
        foreach (var kvp in speciesRegistry)
            stats[kvp.Key.Name] = kvp.Value.Count;
        return stats;
    }
}
```

### E2: Modify `Animal.cs`

```csharp
public void Die(string cause)
{
    if (CurrentState == AnimalState.Dead) return;
    SetState(AnimalState.Dead);
    
    // Register death with Population Manager
    PopulationManager.Singleton.UnregisterAnimal(this, cause);
    
    // Trigger death animation/decay...
}
```

---

## Phase F — Event-Based UI Communication (PRIORITY 3)

**Goal:** UI listens to events instead of polling simulation state.

### F1: Create `GameEvents.cs` (Centralized Event Bus)

```csharp
public static class GameEvents
{
    [Signal] public static event System.Action<SeasonType> OnSeasonChanged;
    [Signal] public static event System.Action<Dictionary<string, int>> OnPopulationUpdated;
    [Signal] public static event System.Action<Animal> OnAnimalSpawned;
    [Signal] public static event System.Action<Animal, string> OnAnimalDied;
    
    public static void TriggerSeasonChanged(SeasonType season) => OnSeasonChanged?.Invoke(season);
    public static void TriggerPopulationUpdated(Dictionary<string, int> stats) => OnPopulationUpdated?.Invoke(stats);
    // ... etc
}
```

### F2: Modify `CameraController.cs` (move to Observer/)

Already pretty clean. Just ensure it doesn't directly access simulation state.

---

## Phase G — Refactor WorldManager (PRIORITY 1)

**Goal:** Reduce WorldManager to ONLY world generation. Remove orchestration.

### G1: New Minimal WorldManager

```csharp
public partial class WorldManager : Node3D
{
    // --- MAP SETTINGS ---
    [Export] public int MapWidth = 50;
    [Export] public int MapHeight = 50;
    [Export] public float TileSpacing = 2.0f;
    // ... noise settings ...
    
    private HexTile[,] worldGrid;
    
    [Export] public VegetationSpawner VegetationSpawner;
    [Export] public SimulationTickManager SimulationTickManager;
    [Export] public SeasonManager SeasonManager;
    [Export] public PopulationManager PopulationManager;
    
    public override void _Ready()
    {
        // ONLY handle world generation
        worldGrid = new HexTile[MapWidth, MapHeight];
        GenerateNoise();
        GenerateWorld();
        BuildTileNeighbours();
        
        // Managers handle their own initialization
        // (They're already in the scene tree)
    }
    
    // Removed: Season timer, Animal spawning, Orchestration
    // Removed: All _Process() logic except world queries
}
```

### G2: Create `GameInitializer.cs`

New class that orchestrates initial spawning:

```csharp
public partial class GameInitializer : Node
{
    [Export] public WorldManager World;
    [Export] public SimulationTickManager SimulationTick;
    [Export] public PopulationManager PopulationManager;
    
    [Export] public PackedScene RabbitScene;
    [Export] public int InitialRabbitCount = 20;
    
    [Export] public PackedScene PredatorScene;
    [Export] public int InitialPredatorCount = 3;
    
    public override void _Ready()
    {
        // Wait for world to generate
        await Task.Delay(100);
        
        // Spawn initial animals
        SpawnSpecies(RabbitScene, InitialRabbitCount);
        SpawnSpecies(PredatorScene, InitialPredatorCount);
    }
}
```

---

## Implementation Order

1. **Step 1:** Create `SimulationTickManager.cs` + remove `_Process()` from Animal/BerryBush
2. **Step 2:** Create `SeasonManager.cs` + remove season logic from WorldManager
3. **Step 3:** Reorganize folders (rename Simulation → Systems, create Environment/Observer)
4. **Step 4:** Create `InteractionManager.cs` + refactor Animal eating logic
5. **Step 5:** Create `PopulationManager.cs` + wire up RegisterAnimal/UnregisterAnimal
6. **Step 6:** Simplify `WorldManager.cs` to only handle world generation
7. **Step 7:** Create `GameInitializer.cs` for initial spawning
8. **Step 8 (Optional):** Create event bus + UI listeners

**Estimated Time:** 3-4 hours with testing.

---

## Testing Checklist

- [ ] World generates correctly
- [ ] SimulationTick runs at fixed rate (animals don't update between ticks)
- [ ] Season changes trigger events
- [ ] Animals eat food through InteractionManager
- [ ] PopulationManager tracks births/deaths correctly
- [ ] Animals can be spawned/despawned dynamically
- [ ] No reference errors between systems
- [ ] Simulation can be paused/resumed

---

## Benefits After Refactoring

✅ Enables 1000+ animals  
✅ Code is testable and debuggable  
✅ Easy to add new systems (weather, disease, migration)  
✅ UI is decoupled from simulation  
✅ Simulation loop is reproducible (save/load)  
✅ Adding new species requires zero changes to existing code  
