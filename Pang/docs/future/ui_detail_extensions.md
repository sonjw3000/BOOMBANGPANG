# Detail UI Future Extensions

This document tracks planned interaction/button expansions for Detail UI.

## Planned Button Expansions

- Worker current task button should later open the task management UI instead of only logging.
- Box item rows should later support an item encyclopedia button.
- Box item rows should later support an order-linked button when a related order exists.
- Worker carrying-box item rows should later reuse the same item encyclopedia / order button pattern.
- PackingStation box item rows should later reuse the same item encyclopedia / order button pattern.
- Other box-based detail UIs should reuse the same box item row interaction pattern.
- Rocket / cargo / related object links may later open their own management or detail windows.

## Planned Building / Zone Detail Expansion

- Building Detail should later show zone-based facility grouping, not only a flat facility count.
- Building Detail should later expose richer zone summaries that are meaningful to player decisions.
- Zone Detail should later show facilities that belong to the zone through `ZoneManager` query APIs.
- Zone Detail should later surface facility mismatch or warning state when installed equipment does not fit the zone's intended role.
- These detail views should support the transition from debug inspection to gameplay-readable operational information.

## Current Intent

- Keep current Detail UI read-focused first.
- Leave button shells or logging hooks where appropriate.
- Reuse common box item UI so later button expansion can be added in one place.
