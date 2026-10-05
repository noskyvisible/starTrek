# MASTER PROMPT — Star Trek Fan Game ("Captain's Chair")

> This is the north-star brief for every build session. Read it before starting any task.
> Tools: **Blender** (via Blender MCP) for all models, **Unity 6.6 (6000.6.4f1)** (via MCP for Unity) for all gameplay.
> This is a non-commercial fan project. Create every asset, sound and piece of music ourselves. Never rip models, audio, music or footage from the films or shows.

---

## 1. The Vision

You are a Starfleet captain. You never fly the ship from a chase camera. You **stand on the bridge and command**, and the ship and crew carry out your orders. Everything happens in first person, inside physical spaces: the bridge, the corridors, the turbolifts, engineering, the transporter room, alien worlds and enemy ships.

The target is AAA in look, sound and play: cinematic lighting, dense hull and interior detail, a full orchestral-style score, and systems deep enough that every battle plays out differently.

**Design pillars:**

| # | Pillar | Fantasy |
|---|--------|---------|
| 1 | **Ship-to-ship combat from inside the ship** | "Shields at 20%! Divert power from the warp core!" You command; the crew executes; the bridge shakes. |
| 2 | **Ground combat and away missions** | Beam down with an away team of phasers and tricorders, scan, solve, fight, then beam back. |
| 3 | **Boarding and being boarded** | Klingons beam onto your bridge, or you lead a team through the red-lit corridors of their ship. |

**The core rule that binds the pillars together:** there is **one simulated ship**. Its systems live in real rooms. A torpedo hit to the starboard nacelle pylon isn't just a health bar. It damages a place you can walk to, fills a corridor with smoke, and cuts the turbolift route. Space combat creates the ground battles that follow.

---

## 2. Setting and Era (canon-locked)

- **Era:** the Star Trek movie era, about **2285** (*The Wrath of Khan* / *The Search for Spock*). This era has the canon Kobayashi Maru, the K't'inga battle cruiser and the Bird-of-Prey, and the most cinematic ship designs.
- **Player ship:** a **Constitution-class (refit)** heavy cruiser with an original name and registry. Placeholder: **USS Resolute, NCC-1741**. Do not use "Enterprise" for the player ship.
- **Enemy:** the Klingon Empire (K't'inga battle cruisers and B'rel Birds-of-Prey).
- **Tone:** cinematic and grounded. Duty, sacrifice, command under pressure. Klingons are honourable, aggressive and smart, not cartoon villains.

---

## 3. Visual Design Bible (research-based; follow it)

### 3.1 Federation: Constitution-class refit
- **Silhouette:** a large flat **saucer** (about 142 m across) on a swept **neck** connected to a rounded "vat-shaped" **secondary/engineering hull**. Two **angular warp nacelles** sit on swept-back **pylons** that join the engineering hull close to the neck. Total length is about 305 m.
- **Hull finish:** off-white with a **pearlescent sheen**. The **"Aztec" panel pattern** uses many subtly different tints of white, silver and grey in interlocking tiles. It gives the hull scale and makes it shimmer as light moves across it. Build it as a tileable mask with slight per-panel colour and roughness changes; never use one flat white.
- **Lighting is a signature:** the ship is **floodlit by its own hull-mounted spotlights**, so the registry and saucer are lit in pools of light. It also has running lights, strobes, and rows of warm window lights.
- **Nacelles:** **blue glowing warp-field grilles** on the inner faces and **red Bussard collector** domes at the front, with an animated swirling interior.
- **Deflector:** a glowing **blue-to-amber deflector dish** on the front of the engineering hull. **Red/amber impulse engines** at the back of the saucer. A **double photon-torpedo launcher** housing at the base of the neck.
- **Detail kit:** phaser emitter banks on the saucer (top and bottom), RCS thruster quads, escape-pod hatches, shuttlebay doors at the back with landing lights, a bridge dome on top of the saucer, a sensor dome underneath, and registry markings (NCC-1741, pennant stripes).
- **Interior design language:** clean and functional 1980s-future. Moulded off-white wall panels, **burgundy/maroon accents** (from the uniforms), grey carpet, recessed edge lighting, and backlit **coloured display panels** with physical buttons. **Red Alert** turns everything red with flashing panels and a klaxon.

### 3.2 Klingon: K't'inga-class battle cruiser
- **Silhouette:** a **bulbous command pod** ("head") at the front of a **long thin neck/boom**. The neck flares into a wide, **wing-like aft hull**, with one **warp nacelle under each wing**. It looks like a bird of prey in flight. It has a larger bridge dome than the older D7.
- **Hull:** **dense, layered armour plating** with heavy greebling. It is grey-green, with weathering, scorch marks and stains: a ship that has seen battle.
- **Lighting:** sparse **amber/orange** window lights and a glowing **red torpedo launcher** in the head. Disruptors flash **green**.
- **Weapons:** green **disruptor bolts** and photon torpedoes from the head. Can cloak and de-cloak, with a shimmering refraction effect.

### 3.3 Klingon: B'rel-class Bird-of-Prey
- **Silhouette:** a compact hull with a **dual-ridged dorsal** section and a **neck leading to the bridge head**. **Movable wings** go up for cruise/landing and down for attack.
- **Wings:** **feather patterns** painted onto the wings, in **green and brown** tones.
- **Weapons:** **rapid-fire disruptor cannons on the wingtips** and a forward torpedo launcher. Has a **cloaking device**.

### 3.4 Klingon interiors (for boarding)
- Dark, tight and industrial. Low ceilings, exposed structure, metal grating floors, angular **triangular/trefoil** motifs, and **red-orange** emergency-style lighting through haze. A raised **command chair** above a sunken bridge pit.
- It must feel like the opposite of the bright, orderly Federation interiors.

### 3.5 Rendering targets
- **Pipeline:** **URP with HDR rendering**. It was chosen over HDRP so development runs smoothly on the Intel UHD 620 laptop. Use HDR colour, bloom, tone mapping, lens flares, decals, SSAO and post-processing volumes. Where HDRP would use real volumetrics, fake them with fog cards, light-shaft meshes and particle haze.
- **Space:** a dense HDR starfield, nebula volumes, a nearby planet or sun as the key light, bloom on engines and weapons, lens flare, and screen-space light scatter (faked light shafts).
- **Combat VFX:** phaser beams that **trace across the hull** to the target, torpedoes as glowing pulsating orbs, **shield bubbles that light up at the impact point** and ripple out, hull breaches that vent atmosphere and burning debris, and a warp jump with streaking starlight.
- **Bridge damage:** consoles that **explode in sparks**, falling ceiling panels, crew thrown from their stations, smoke layering in the room, emergency lighting, and camera shake.
- **Quality tiers (required):** **Ultra** (dedicated GPU: high-resolution shadows, SSAO, MSAA, all post effects), **High**, and **Low** (must run on the Intel UHD 620 development laptop: baked lighting, cheap shadows, no SSAO, minimal post-processing, render scale about 0.7). Every feature must degrade gracefully.

---

## 4. Pillar 1 — Ship Combat from the Captain's Chair

### 4.1 Camera and control
- **First person only**, always inside the ship. The player can sit in the captain's chair or walk around the bridge to stand at a station.
- The **main viewscreen** shows the battle and can switch views: forward, aft, target lock, and magnified tactical view. Bridge windows and the viewscreen are the main view of space.
- **Orders** are given through:
  1. a **radial command wheel** (keyboard/mouse and controller),
  2. **walking to a station** and using its console for fine control,
  3. *(stretch)* **voice commands** ("Full impulse", "Fire phasers", "Red alert").
- The ship is **flown by the AI helm officer** following the captain's orders: headings, speeds, attack patterns ("bring us about", "evasive pattern Delta", "close to 10,000 km", "keep the target off our port bow"). The ship moves through a real 3D simulation; the player just isn't holding the stick.

### 4.2 The bridge crew (AI officers)
| Station | Officer role | Example orders |
|---|---|---|
| Helm | Steering, speed, manoeuvres | Full impulse, come about, evasive pattern, ram |
| Navigation | Courses, warp jumps | Set course, warp 6, plot an escape vector |
| Tactical | Weapons and shields | Target their weapons/engines, fire at will, raise shields |
| Science | Sensors, scans | Scan for weaknesses, locate cloaked ship, life signs |
| Communications | Hailing, distress calls | Open hailing frequencies, jam their comms, call Starfleet |
| Engineering (Chief Engineer, via intercom) | Power and repairs | More power to shields, eject the warp core |

Officers **talk back** with acknowledgements, reports and warnings. They **get injured** and can be swapped out. A missing officer means a worse response at that station.

### 4.3 Ship systems (all simulated; all have a physical location on board)
- **Warp core (M/ARA):** a vertical matter/antimatter reactor that runs through several decks in engineering. It provides the **power budget**. Its states: nominal, overloaded, breach risk, ejected.
- **Power distribution:** spread power between shields, weapons, engines and life support. Overloading something brings risks.
- **Impulse engines** for combat speed and turning. **Warp drive** to arrive, escape or fail to escape.
- **Deflector shields:** **4 facings** (fore, aft, port, starboard), each with its own strength and regeneration. They can be strengthened on one side.
- **Phasers:** banks with arcs of fire that recharge, can be aimed at **specific enemy systems**, and can overheat.
- **Photon torpedoes:** a limited supply, a load/arm cycle, and spreads.
- **Sensors:** detection range, scan detail, and finding cloaked ships by their tell-tale traces.
- **Structural integrity field** and **inertial dampers**: if they fail, the crew is thrown around.
- **Life support**, **transporters** (needs shields down on the side facing the target), **tractor beam**, and **comms**.
- **Klingon-only:** a **cloaking device**. They cannot fire while cloaked, and they're vulnerable when they de-cloak.

### 4.4 Damage model
- Hits are tracked **per shield facing**, then **per hull section**. Each section contains real rooms and systems.
- Damage creates **incidents** inside the ship: fires, hull breaches, blocked corridors, injured crew, broken turbolift segments, and plasma-conduit ruptures.
- **Damage-control teams** are sent by order and walk physically to the damage. The player can go personally (see Pillar 3), at the cost of not being on the bridge.

### 4.5 Combat feel
- Fights are **tactical and cinematic**: positioning, shield facing, timing torpedoes, and managing power. It's not twitch shooting.
- Each hit hits the **bridge**: shake, sparks, lights flickering, the crew reacting, and audio muffling.

---

## 5. Pillar 2 — Ground Combat and Away Missions

- **Beaming in and out:** a transporter sequence with shimmering particles and a sound swell. You can only beam if the ship has a lock and the shields facing the target are down, so ship status matters on the ground.
- **Away team:** the captain plus 2–4 crew (security, science, medical, engineering) with **squad orders**: move, hold, cover, scan, heal, hack.
- **Phaser:** a beam weapon with **stun and kill settings**, which affect mission outcomes. Overheats; can be used to cut, weld or heat things.
- **Tricorder:** **scanning is a core mechanic**. Life signs through walls, traps, weak points, clues and environmental hazards.
- **Communicator:** call the ship: "Beam us up", "Scan this area", orbital phaser support (if the ship has power and position).
- **Combat:** cover-based first person with flanking AI and suppressing fire. Klingon enemies **close in for melee with bat'leth / d'k tahg**, so close combat must feel heavy.
- **Mission structure:** investigation, rescue, sabotage and first contact. Not every mission needs combat.

---

## 6. Pillar 3 — Boarding and Being Boarded

### 6.1 Defending your ship
- Klingons **beam aboard** at key places (bridge, engineering, the torpedo bay) when your shields fail on a side facing them.
- **Intruder alert:** the ship map shows hostile life signs moving deck by deck. Send security teams, **raise force fields** at corridor junctions, seal bulkheads, flood decks with anaesthizine gas.
- The captain can **fight personally** on the bridge (phaser and hand-to-hand) or lead a counter-attack via the turbolift.
- Losing engineering means the Klingons can **sabotage the warp core**, which starts a countdown and a desperate fight.

### 6.2 Boarding the enemy
- Beam over (needs **their shields down**) or dock (stretch).
- Objectives: **capture the bridge**, **sabotage the cloak or disruptors**, **rescue prisoners**, **set a warp core overload** and get out.
- Klingon interior (section 3.4): tight corridors, flanking side passages, warriors who **charge rather than hide**.

---

## 7. Moving Around the Ship — Turbolifts and Layout

- The ship interior is a **real, walkable, connected space** at real-world scale (1 Unity unit = 1 m). Starting deck list:
  - **Deck 1:** Bridge
  - **Deck 4–5:** Officers' quarters, briefing room
  - **Deck 7:** Sickbay
  - **Deck 8:** Transporter room
  - **Deck 11:** Phaser control / Security
  - **Deck 15:** Engineering (warp core spans several decks)
  - **Deck 16:** Torpedo bay
  - **Deck 19:** Shuttlebay
- **Turbolifts:**
  - A **network of shafts** with vertical and horizontal travel between named stops. The cars move along real paths through the ship.
  - **Entering a car:** grab the **control handle** and say or choose a destination ("Bridge", "Engineering", "Deck 7"). The doors close with the turbolift's trademark whoosh, and you hear the rising whine of travel.
  - **Travel:** lights streak past the car's window slits; movement changes direction with a small jolt; arrival chimes.
  - **Technical role:** turbolift rides are a **cover for loading** other deck areas (async additive scenes), so the ship can be huge without loading screens.
  - **Systems role:** shaft segments can be **damaged**, which reroutes or blocks rides. Turbolifts can be **locked out** against boarders. Emergency **Jefferies tubes** are a crawlspace alternative.

---

## 8. Test Level — The Kobayashi Maru (Vertical Slice)

The first playable level and the quality benchmark for everything else. It introduces all three pillars in one session.

**Setting:** the **Starfleet Academy simulator**, a full-scale replica of the refit bridge. It has an **instructors' observation gallery** behind one-way glass. You are a **cadet captain** commanding a simulated crew of fellow cadets.

**Canon details:**
- The **Kobayashi Maru** is a **Class III neutronic fuel carrier** with a **crew of 81 and 300 passengers**. It has hit a gravitic mine and lost all power. It is stranded in **Gamma Hydra, Section 10**, inside the **Klingon Neutral Zone**.
- If the cadet enters the Neutral Zone, **three Klingon K't'inga-class battle cruisers** de-cloak and attack.
- The test is built to be **unwinnable**. It measures **character under pressure**, not victory.

**Beat sheet:**
1. **Arrival:** briefing in the simulator room. Walk the corridor, enter the turbolift, take the chair. Tutorial for the command wheel and stations.
2. **The distress call:** "Imperative, this is the Kobayashi Maru…" The audio breaks up. Science reports that entering the Neutral Zone breaks the treaty. **First choice:** go in, or leave them.
3. **The rescue attempt:** reach the Maru. Science scans for survivors. **Away beat (Pillar 2):** beam a small team over to the darkened, damaged freighter to free trapped crew. Tricorder use, a short fight against a hazard, beam back.
4. **The ambush:** **three K't'inga cruisers de-cloak** (big moment). Full ship combat (Pillar 1). They are tuned to win: smart AI that targets shields and steadily overwhelms you.
5. **Boarded (Pillar 3):** with shields down, **Klingons beam onto the bridge**. Short, brutal close-quarters fight at the stations.
6. **No-win:** the systems fail one after another. The crew "die". The warp core reaches breach. The player chooses: **fight to the end, surrender, self-destruct to take an enemy with them, or retreat** and leave the Maru.
7. **End of simulation:** the screens freeze, the house lights come on, and the instructor's voice comes over the speaker. The **evaluation screen** grades decisions, crew care, composure and choices, never "victory".
8. **Easter egg:** a hidden way to "**reprogram the simulation**" before the test (a terminal in the Academy hallway). Doing this lets you win, and the evaluation calls it out.

---

## 9. Audio Direction (AAA)

- **Middleware:** FMOD Studio (free indie licence) for adaptive music and layered SFX. If that becomes too much to wire up, use Unity Audio.
- **Score:** an **original** orchestral score: noble brass and strings for the Federation themes, low brass, timpani and percussion for the Klingons. **Adaptive stems** that react to combat intensity, Red Alert and victory/defeat.
- **Signature sounds (create originals in the spirit of the genre; do not copy):** the bridge background hum and console beeps, door swish, the turbolift whine, red alert klaxon, phaser beam, torpedo launch, shield impacts, transporter shimmer, the warp jump, Klingon disruptors, and the de-cloak.
- **Spatial:** sounds come from where they happen. Impacts come from the side that was hit. Engineering is loud with the warp-core throb.
- **Voice:** crew acknowledgements and reports in many variations so they don't repeat. Klingon barks in Klingon and English. Use placeholder text-to-speech until real voices exist.
- **Mixing:** hits **muffle** the sound briefly and add ringing. Red Alert ducks the music.

---

## 10. Asset Pipeline — Blender → Unity

- **Units and scale:** 1 Blender unit = 1 m. Forward = −Y in Blender → +Z in Unity (export with the matching FBX axis settings). Apply all transforms before exporting.
- **Naming:** `SHIP_Constitution_Hull_LOD0`, `PROP_Bridge_CaptainChair`, `CHAR_Klingon_Warrior`, `ENV_Corridor_Straight_4m`.
- **Modular interiors:** a kit of corridor, wall, door and room pieces on a **2 m grid**, so the ship can be assembled quickly in Unity.
- **Textures:** **trim sheets and tileable materials** for the hull (Aztec mask plus panel lines plus emissive window masks). Use unique bakes only for hero props (captain's chair, warp core, consoles). PBR metallic/roughness, URP Lit.
- **Emissive maps** for windows, nacelle grilles, Bussard collectors, the deflector, console screens and LCARS-style displays (create our own display style rather than copying Okuda).
- **LODs:** LOD0–LOD3 for every ship, plus a far-distance impostor.
- **Damage:** ship hull sections are separate pieces so they can **break apart** and show **damage-decal** states.
- **Bridging:** MCP for Unity has a built-in **Blender bridge** to the BlenderMCP addon. Use it to move models straight from Blender into the Unity project.

**Project folders (Unity):**
```
Assets/_Project/
  Art/Ships/{Federation,Klingon}/  Art/Interiors/  Art/Characters/  Art/VFX/
  Audio/{Music,SFX,VO}/
  Scenes/{Bootstrap,KobayashiMaru,Ship_Decks/*}/
  Scripts/{Ship,Crew,Combat,Ground,Boarding,Turbolift,UI,Audio,Core}/
  Prefabs/  Materials/  Settings/(URP quality tiers)
```

---

## 11. Technical Architecture (Unity)

- **Ship simulation core** (`ShipSystems`): power, shields, weapons and hull sections as **data-driven ScriptableObjects**, separate from visuals. Both the player ship and enemy ships use it.
- **Command system:** orders are queued command objects → **AI officer** → system action. The same system drives voice, radial and console input.
- **Two linked spaces:**
  - **Space scene:** ships at real distances, with a floating-origin system to keep precision correct.
  - **Interior scene:** the walkable ship, loaded in additive chunks via turbolifts.
  The interior gets combat events (impacts, shakes, damage) from the space simulation.
- **AI:** behaviour trees or utility AI for the Klingon ship captains, ground squads and boarders.
- **Save:** a ship-state snapshot plus mission flags.
- **Performance budgets:** 60 fps at 1440p on the High tier (RTX 3070 class). 30 fps at 720p on the **Low tier (Intel UHD 620 development laptop)**.

---

## 12. Build Order (Milestones)

1. **Foundations:** URP project (HDR), quality tiers, folder structure, first-person controller, interaction system.
2. **The bridge:** a hero-quality refit bridge (Blender), lighting, working consoles and viewscreen, Red Alert state.
3. **Ship simulation and space combat:** player ship and one K't'inga (graybox models first), shields, phasers, torpedoes, helm AI, command wheel.
4. **Turbolift and the deck network:** bridge ↔ turbolift ↔ corridor ↔ engineering with the warp core.
5. **Boarding:** Klingons beam onto the bridge, close-quarters combat, force fields.
6. **Away team:** a derelict Kobayashi Maru interior, tricorder, phaser stun/kill, squad orders.
7. **Kobayashi Maru level:** the full beat sheet, scripted events, evaluation screen, easter egg.
8. **AAA polish:** hero ship models (full Aztec hull and lighting), VFX, score, the sound mix, crew voices.

**Every milestone ends with a playable build and a short video/screenshot check.** Gray-box first, then make it beautiful. Don't polish something that isn't fun yet.

---

## 13. Working Rules for Claude

- Use **Blender MCP** for modelling, then the Blender bridge or FBX export into Unity. Use **MCP for Unity** for scenes, prefabs, scripts and testing.
- Before modelling any canon ship or set, re-check its design notes in section 3 and look up references. Authenticity matters.
- Keep systems **data-driven** and **separate from visuals**, so a gray box can be swapped for a hero asset without code changes.
- After each change, **run it in the editor and check** with a screenshot or play mode. Report honestly what works and what doesn't.
- Respect the **Low quality tier**. Development happens on an integrated-GPU laptop.
- Commit at each milestone with a clear message.
