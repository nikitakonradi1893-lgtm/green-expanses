# Internal First Playable scope

This is an internal developer-facing playable slice, not the full MVP acceptance gate.

It must use the real Domain/Simulation/Application/Persistence code paths and provide:

- New Game: `inheritance` scenario from a numeric seed.
- One owned field selected near the 30 ha target without modifying world distributions.
- A provisional, data-driven starting cash reserve until DifficultyProfiles are implemented.
- Home summary: date, cash, owned area, world field count.
- Fields view with the owned field agronomic baseline.
- Advance one day / advance seven days through simulation API.
- Save and Load through Save v1 autosave.
- No direct mutation of domain state from Godot controls.

The full MVP season flow (operations, machinery, weather, crop cycle, harvest, market and annual report) remains governed by the milestone acceptance gates in the Drive technical specification.