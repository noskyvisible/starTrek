# Star Trek fan game — "Captain's Chair"

Read `docs/GAME_PROMPT.md` before any task. It is the design brief and the source of truth.

## Environment
- All work is local. Don't use cloud sessions for this project.
- Unity **6000.6.4f1** with **URP (HDR)**, project root = repo root. Gameplay code lives in `Assets/_Project/Scripts/...` (folder layout in GAME_PROMPT §10).
- MCP for Unity (`UnityMCP`, HTTP on 127.0.0.1:8080) and Blender MCP (Blender 5.2, addon on port 9876). Start Unity and Blender before Claude Code, or their tools won't load.
- Dev machine: Intel UHD 620 laptop. Respect the Low quality tier; avoid per-frame allocations.

## Working rules
- **Keep the user able to watch.** Build in the open in Blender and Unity, and point the viewport at the current work.
- **Devlog:** add to `devlog/YYYY-MM-DD.md` as work happens (Done / Decisions / Problems / Next), with screenshots in `devlog/images/`.
- **Art source:** .blend files go in `Art_Source/Blender/`, outside `Assets/`. Export to Unity through the MCP for Unity Blender bridge (glb).
- **Secrets:** API keys live in `.secrets/` (git-ignored) and in the Unity secure key store. Never commit, print or paste a key.
- **AI generation:** Tripo (via `generate_model`) is for textured 3D hero props, used only after the graybox plays well. Generated assets must be original. Never imitate film or show assets.
- Systems are data-driven and separate from visuals (GAME_PROMPT §11, §13). Keep simulation logic in plain C# where practical, so it can be unit-tested.
- 1 Unity unit = 1 m. Namespaces start with `StarTrek.` (e.g. `StarTrek.Ship`, `StarTrek.Player`).
