# Audio sources and licence log

**Rule (GAME_PROMPT §9, CLAUDE.md):** no audio, music or effects from any Star Trek film or series. Sound effects come from free-licensed libraries (logged here) or are made by us. Music and signature sounds are original, generated or composed for this project.

## Libraries in the project

| Pack | Files | Source | Licence | Attribution | Location |
|---|---|---|---|---|---|
| Kenney Sci-Fi Sounds 1.0 | 73 | https://kenney.nl/assets/sci-fi-sounds | CC0 1.0 (public domain) | Not required (credit appreciated) | `Assets/_Project/Audio/SFX/Kenney/sci-fi-sounds` |
| Kenney Interface Sounds | 100 | https://kenney.nl/assets/interface-sounds | CC0 1.0 | Not required | `Assets/_Project/Audio/SFX/Kenney/interface-sounds` |
| Kenney Impact Sounds | 130 | https://kenney.nl/assets/impact-sounds | CC0 1.0 | Not required | `Assets/_Project/Audio/SFX/Kenney/impact-sounds` |

Each pack folder keeps its original `License.txt`. Downloaded 2026-10-05.

## Made by us

| Sound | How | Where |
|---|---|---|
| Door whoosh, button chirp, denied tone, red-alert klaxon (fallbacks) | Synthesised at runtime | `Scripts/Audio/ProceduralSfx.cs` |

## Generated (original)

| File | Tool | Prompt summary | Date | Notes |
|---|---|---|---|---|
| *(none yet)* | | | | |

## Candidate sources for later (check the licence on each file before use)
- **Freesound** (https://freesound.org): filter by **CC0**; CC-BY needs attribution, so log the author. Needs an account to download.
- **OpenGameArt** (https://opengameart.org): filter by CC0; mixed licences, check each.
- **Sonniss GameAudioGDC bundles** (https://sonniss.com/gameaudiogdc): royalty-free for use in games; the licence forbids redistributing the raw files, which is fine inside a built game. Large downloads.
- **Pixabay sound effects** (https://pixabay.com/sound-effects/): Pixabay Content License (free use, no attribution). The raw files can't be redistributed standalone, so keep them inside the project only.
- Avoid **BBC Sound Effects** (RemArc licence is personal/educational/research only).
