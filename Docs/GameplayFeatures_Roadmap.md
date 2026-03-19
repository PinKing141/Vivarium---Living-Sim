
# 🌍 LivingSim Ecosystem Development Roadmap

## Phase 1 — World Generation Foundation (Complete)

*Goal: Generate a believable natural world.*

✅ Hex grid world generation
✅ Tile spacing and hex math positioning
✅ Noise-based elevation and moisture generation
✅ Biome system (Ocean, Beach, Grassland, Forest, Desert, Mountains)
✅ Biome blending / edge dithering
✅ Vegetation spawning system
✅ Vegetation variation arrays (multiple tree/rock/bush models)
✅ Vegetation clustering using noise maps
✅ Berry bush food resources spawned in the world
✅ Tile data structure created (`HexTile.cs`)
✅ Hex grid memory array established
✅ Hex neighbour lookup system
✅ Basic environmental lighting setup

---

## Phase 2 — Tile Simulation Layer (Complete)

*Goal: Make the terrain tiles aware of the physical objects and actors sitting on them so they can answer queries from animals.*

✅ **Tile vegetation tracking**
✅ Tile animal tracking
✅ Tile food queries (GetFoodSource, FindNearestFood)
✅ Tile neighbour queries (GetWalkableNeighbours, GetNeighbourClosestTo, GetNeighbourFurthestFrom)

---

## Phase 3 — Animal Core System (Complete)

*Goal: Introduce the first living creatures to the world.*

✅ Base `Animal.cs` class creation
✅ Placeholder 3D models (Rabbit/Fox) integration
✅ Core stats: Hunger, Energy, Movement Speed, Vision Radius, Eating Distance
✅ Full state machine (Idle → Wandering → SearchingFood → Hunting → Fleeing → Eating → Reproducing → Dead)

---

## Phase 4 — Movement & Tile Navigation (Complete)

*Goal: Allow animals to physically traverse the hex grid.*

✅ Tile-to-tile movement logic (ProcessMovement, targetTile system)
✅ Smooth 3D movement interpolation between hexes (MoveToward + lerp rotation)
✅ Neighbour selection logic (GetNeighbourClosestTo, GetRandomWalkableNeighbour)
✅ Tile occupation updating (Animals list management on CurrentTile)

---

## Phase 5 — Resource Consumption (Complete)

*Goal: Connect the animals to the environment so they can survive.*

✅ Food search behaviour (SearchingFood state, FindNearestFood)
✅ Target reservation (targetFood/targetPrey tracking per animal)
✅ Eating animation/behaviour (EatAnim, ProcessEating state)
✅ Food depletion (BerryBush.EatBerry(), vegetation removal)
✅ Resource regeneration (BerryBush timer with RegrowTime = 30s)

---

## Phase 6 — Population Simulation (Complete)

*Goal: Turn individual animals into a dynamic species.*

✅ Energy expenditure mechanics (CurrentHunger -= delta * HungerDrainRate)
✅ Starvation and death (DieStarvation, BeKilled methods)
✅ Reproduction system (GiveBirth, Reproducing state, ReproductionThreshold = 85%)
✅ Lifetime tracking (ageing) — MaxAge/CurrentAge with death at max age

---

## Phase 7 — Predator / Prey Ecosystem (Complete)

*Goal: Introduce the food chain.*

✅ Carnivore animal class (DietType.Carnivore with Fox preset)
✅ Hunting behaviour state (Hunting state, FindNearestPrey, targetPrey tracking)
✅ Fleeing/evasion behaviour state for herbivores (Fleeing state, FindNearestPredator)
✅ Vision/Detection radius for predators (VisionRadius parameter)
✅ Territory logic (HasTerritory toggle, TerritoryRadius, TerritoryCenter enforcement)

---

## Phase 8 — Environmental Evolution (Complete)

*Goal: Make the world itself dynamic and changing.*

✅ Seasonal cycles (Spring, Summer, Autumn, Winter) — 60 second per season cycle
✅ Seasonal visual changes (snow/standard mesh swapping in SeasonalTerrain & SeasonalPlant)
✅ Water sources and animal thirst mechanics (SearchingWater state, IsNextToWater(), FindNearestWater(), death by dehydration)

---

## Phase 9 — Simulation Systems (Current Phase)

*Goal: Add critical regulation and management systems to prevent population collapse/explosion and enable genuine emergent behaviour.*

✅ **Genetic Variation System** — Traits mutate across generations (speed, vision, hunger rate, age, reproduction)
✅ **Species Manager** — Track population stats (birth rate, death rate, extinction detection, species counters)
✅ **Simulation Tick Manager** — Decouple behavior from frame rate (fixed tick rate for stability)
✅ **Event System** — Dispatch events (OnAnimalBorn, OnAnimalDeath, OnPlantConsumed, OnSeasonChanged) for UI/stats
✅ **Resource Reservation System** — Prevent 20 animals competing for same food (claim/unclaim mechanic)
✅ **Hex Pathfinding** — A* or flow field for long-distance navigation (navigate around water/obstacles)

---

## Phase 10 — Simulation Scaling

*Goal: Optimise the code so the ecosystem can run massive numbers of entities.*

✅ Animal Level of Detail (LOD) simulation (background simulation for animals off-camera)
✅ Object pooling for animals and plants
✅ Chunk loading for massive map sizes

---

## Phase 11 — Observer Gameplay

*Goal: Give the player tools to watch and interact with the simulation.*

✅ Free-cam observer controls
⬜ Animal inspection UI (click an animal to see its stats, age, and hunger)
⬜ Time controls (Pause, 1x, 2x, 5x speed)
⬜ Ecosystem statistics graphs (population trackers)

---

## Future Expansions (Nice-to-Have, Not Required)

*Features that add depth but aren't essential for core simulation.*

🔮 **Migration System** — Animals relocate when local food becomes scarce (seek richer biomes)
🔮 **Social Behaviour** — Packs, herds, flocking for group species
🔮 **Disease System** — Infection spread, recovery, death for population regulation
🔮 **Weather System** — Rain, drought, heatwaves, snowstorms affecting plant growth and animal behaviour
🔮 **Plant Spreading** — Trees drop seeds to neighbouring tiles (expansion mechanic)
🔮 **Advanced Territory System** — Territory marking and conflict between rivals
