# Reference: the Kobayashi Maru (ship and interior)

For the Kobayashi Maru test level (GAME_PROMPT §8): the derelict freighter seen from the bridge, and the walkable interior for the away-team beat.
Links and written notes only; no copyrighted images or models are stored in this repo.

## 1. Canon facts (with sources)

| Fact | Source |
|---|---|
| The *Kobayashi Maru* is a **civilian fuel vessel** stranded in the neutral territory between the Federation and the Klingon Empire. The test is whether a cadet risks ship and crew to rescue it or leaves it. | [Wikipedia: Kobayashi Maru](https://en.wikipedia.org/wiki/Kobayashi_Maru) |
| It is a **Class III neutronic fuel carrier**; our brief also uses **81 crew and 300 passengers**, **Gamma Hydra, Section 10**, and a **gravitic mine** (GAME_PROMPT §8). | [Memory Alpha: Class III neutronic fuel carrier](https://memory-alpha.fandom.com/wiki/Class_III_neutronic_fuel_carrier) (returned HTTP 402 to our fetcher; the class name also appears in the search summaries) |
| **The ship is never actually shown in *The Wrath of Khan*.** It appears only as data on the simulator screen. | [Search summary of Memory Alpha / Model Citizen](https://employees.csbsju.edu/rsorensen/modelcitizen/trekships/virtual/KM/default.html) |
| On screen in TWOK: a distress call, the cadet enters the Neutral Zone to help, contact with the freighter is lost, **three Klingon ships attack**, the bridge crew are "killed", the cadet captain orders abandon ship, and the simulation ends. | [Wikipedia](https://en.wikipedia.org/wiki/Kobayashi_Maru) |
| Kirk beat the test on his third try by **reprogramming the simulation** so a rescue was possible (our easter egg, GAME_PROMPT §8 beat 8). | [Wikipedia](https://en.wikipedia.org/wiki/Kobayashi_Maru) |
| Later on-screen appearances of the test: *Star Trek* (2009, alternate timeline), *Discovery* S4 premiere "Kobayashi Maru" (2021), *Prodigy* "Kobayashi" (2022). | [Wikipedia](https://en.wikipedia.org/wiki/Kobayashi_Maru) |

### Known visual interpretations (not canon for our era, reference only)
- **Fan design, 1982–83** (published with Starstation Aurora): a "tramp steamer" civilian cargo/passenger ship. It has a **primary saucer** for passengers and cargo, a **small secondary hull** housing the deflector, "big, old and klunky" warp engines, and **shielded outboard pontoons carrying the neutronic fuel tanks**, jettisonable for safety. Proportions were taken from the TWOK data screen. John Eaves used these designs as reference for the 2009 film's version. [The Model Citizen](https://employees.csbsju.edu/rsorensen/modelcitizen/trekships/virtual/KM/default.html)
- Search summaries also describe a later (Kelvin-era / licensed) version: **~259 m**, similar in layout to a *Miranda*, with a compact secondary hull on the back of the saucer, six lettered cargo bay doors, nacelles on top of the pylons, and two ventral cargo pontoons with up to 12 pods (rectangular sealed cargo pods or cylindrical cryogenic fuel tanks). Registry given as NCC-S3700. [Memory Alpha search summary](https://memory-alpha.fandom.com/wiki/Kobayashi_Maru) · [Memory Beta: ECS Kobayashi Maru](https://memory-beta.fandom.com/wiki/ECS_Kobayashi_Maru)
- Other fan/production art: [Thomas Marrone, ArtStation](https://thomasmarrone.artstation.com/projects/34yJJ?album_id=49779) (403 to our fetcher) · [Scifi-Meshes: from fan blueprints](https://forums.scifi-meshes.com/discussion/40197/kobayashi-maru-from-fan-blue-prints) · [DeviantArt: Class 3 neutronic fuel carrier](https://www.deviantart.com/roverdogeryan/art/Class-3-Neutronic-Fuel-Carrier-1114934077)

## 2. Our original design brief (movie era, 2285)

The goal is to stay consistent with canon (a civilian Class III neutronic fuel carrier) without copying any specific existing model. The common ideas are fair game to reinterpret: a working-class freighter, and fuel carried in jettisonable outboard tanks.

- **Role and feel:** an ageing civilian fuel hauler with passenger berths. Utilitarian and patched, the opposite of a Starfleet ship. Industrial greys, not pearl white.
- **Silhouette (~190 m long, ~95 m wide):**
  - forward **command and passenger module**: a squat, flattened lozenge rather than a true saucer, with a small bridge blister on top and rows of passenger windows on the rim
  - a short, thick **spine** with docking collars and lettered **cargo doors A–F**
  - **two outboard fuel pontoons** on stubby struts, each holding a rack of **cylindrical neutronic fuel tanks** with hazard banding; tanks are individually jettisonable
  - **two blunt, boxy warp nacelles** on the dorsal aft spine (older and heavier than Starfleet's), with dull amber field grilles
  - aft **engineering block** with impulse vents and a radiator fin array
- **Palette and markings:** hull grey-blue with darker panel patches and repair plates; **yellow and black hazard chevrons** on the fuel tanks and pontoon struts; merchant-style lettering "KOBAYASHI MARU" on the module flank, plus our own invented registry (see open questions). No Starfleet pennant.
- **Damage state (after the gravitic mine):**
  - **no power** (all windows dark except a few flickering emergency strips)
  - a **torn breach on the aft port pontoon**, with one tank missing and venting fuel vapour (a particle plume with a cold blue-white glow)
  - scorched plating and slow tumbling debris
  - running lights out; one strobe still blinking
- **On the viewscreen:** a graybox first (simple primitives), then a Blender hero model with LODs following GAME_PROMPT §10. Fuel tanks are separate objects, so they can be jettisoned or explode.

## 3. Interior for the away mission (walkable, scaled for gameplay)

Canon shows no interior, so all of this is ours. It's a civilian merchant style: exposed conduits, stencilled signage, worn deck plating, cramped but scaled to our movement rules (walkways ≥ 1.2 m, corridors ~3 m, doors 1.6 × 2.4 m). It's dark: emergency lighting only, with the beam of the away team's lights, haze and frost.

| # | Space | Size (approx.) | Purpose in the beat |
|---|---|---|---|
| 1 | **Cargo Hold B** (beam-in point) | 16 × 12 × 6 m | Arrival. Stacked cargo pods, drifting debris, frost. Tricorder tutorial: life signs ahead, radiation hazard. |
| 2 | **Spine corridor** | 3 m wide, ~30 m | Breached section with venting atmosphere (push force plus a hiss), sparking conduits. A jammed door needs phaser cutting or a tricorder bypass. |
| 3 | **Passenger deck** | 4 cabins + lounge, ~14 × 10 m | **Rescue objective:** survivors trapped behind a buckled pressure door; injured crew. Mark the transport lock. |
| 4 | **Fuel control / engineering** | 12 × 10 m, two levels | Dead reactor and neutronic fuel handling. Radiation hazard zone (timer). The optional "stabilise the tanks" objective prevents a later explosion. |
| 5 | **Freighter bridge** | 8 × 6 m | Small civilian bridge: injured captain, ship's log (the distress call in our own words), sensor logs hinting at cloaked ships. |

Flow: 1 → 2 → 3 (main rescue) with an optional branch 2 → 4 → 5 (lore and hazards), then beam back from 1 or 3. Roughly 6–10 minutes.

## 4. The Klingon side of the scenario

- **Three K't'inga-class battle cruisers** de-cloak once the player commits to the Neutral Zone near the freighter (GAME_PROMPT §8 beat 4). The simulation already holds them cloaked near the freighter. Science's cloak sweep reports faint ion traces when in range.
- They are tuned to win: they target shields, then the bridge, and finally **beam boarders onto the bridge** when shields fail.

## 5. Open questions (where canon is silent)

- **Registry:** NCC-S3700 appears in later licensed/Kelvin-era sources; for the movie era we could keep the name only, or use an invented merchant registry (e.g. "NAR-3700"). Decision needed.
- Exact size and layout of the TWOK-era ship (canon gives none).
- Whether any crew appear in person (canon: only the distress voice). Our interior adds survivors for the rescue beat.
- How a fuel carrier also carries 300 passengers; our answer is a dedicated passenger deck in the forward module.
