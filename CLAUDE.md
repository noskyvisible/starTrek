# Star Trek fan game — "Captain's Chair"

Read `docs/GAME_PROMPT.md` before any task. It is the design brief and the source of truth.

## Environment
- Unity **6000.6.4f1**, project root = repo root. Gameplay code lives in `Assets/_Project/Scripts/...` (folder layout in GAME_PROMPT §10).
- Local sessions have MCP for Unity (`UnityMCP`, HTTP on 127.0.0.1:8080) and Blender MCP. Start Unity before Claude Code, or the Unity tools won't load.

## Cloud sessions (no Unity Editor, no Blender)
Cloud sessions can't open the Editor, compile Unity code, enter Play mode or use either MCP. So:
- Write **code and docs only**: no scenes, prefabs, materials or binary assets.
- **Do not create `.meta` files.** The local Editor generates them and they get committed from there.
- Keep simulation logic in **pure C# with no `UnityEngine` references** (assembly definitions with `"noEngineReferences": true`). Unity-facing code (MonoBehaviours, ScriptableObject wrappers) stays a thin layer on top.
- Check the pure C# code with `dotnet` from a test project **outside `Assets/`** (e.g. `Tools/SimTests/`) that includes the core source files. Add a `.gitignore` exception for that `.csproj`, because `*.csproj` is ignored. Run the tests before you finish.
- Also write Unity EditMode tests for Unity-facing code. They get run locally.
- Work on a branch and open a PR. In the PR, say what was verified with dotnet and what still needs checking in the Editor.

## Conventions
- 1 Unity unit = 1 m. Namespaces start with `StarTrek.` (e.g. `StarTrek.Ship`, `StarTrek.Crew`).
- Systems are data-driven and separate from visuals (GAME_PROMPT §11, §13).
- Respect the Low quality tier (Intel UHD 620 laptop): avoid per-frame allocations in simulation code.
