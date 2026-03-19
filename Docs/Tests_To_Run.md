# Vivarium Test Checklist

Use this list to verify behavior after changes. Run the top section every time, then sample the rest depending on what changed.

## Quick Smoke Tests (always)
1. Launch the game from the editor and confirm it loads past the splash screen.
2. Confirm the HUD appears (season + animal count).
3. Confirm camera controls: WASD move, mouse look, orbit toggle, zoom.
4. Let the sim run for 2-3 minutes and confirm no crashes.

## Movement + Navigation
1. Animals move smoothly between tiles without spinning in place.
2. Animals do not walk through trees or rocks, but do pass through bushes.
3. Pathing re-routes when blocked and does not get stuck.
4. Wandering feels natural (no tight S wiggle, no jitter).
5. Animals pause to rest (not constant motion).

## Needs + Survival
1. Thirst drops over time and animals seek water.
2. Animals drink when adjacent to water and thirst resets.
3. Hunger drops over time and animals seek food.
4. Animals eat when at food source and hunger resets.
5. Death occurs from starvation/dehydration at expected times.

## Reproduction + Life Stages
1. Males/females are both present.
2. Mating only happens between compatible sexes/species.
3. Pregnancy timer triggers birth.
4. Newborns spawn at baby scale and grow through stages.
5. Reproduction cooldown prevents immediate re-mating.

## Population + Species
1. Population counts update when animals spawn/die.
2. Species extinction is logged when the last animal dies.

## World + Biomes
1. Macro biomes appear as large regions (not only small patches).
2. Biome edges blend naturally.
3. Water/mountains are not walkable.

## Chunk Loading + Pooling
1. Chunks load around the camera and unload outside radius.
2. No spikes or stalls when moving the camera quickly.
3. Pooled animals/vegetation despawn and respawn cleanly.

## UI + Events
1. Season changes update in the HUD.
2. Event logs (birth/death/consumption) appear as expected.

