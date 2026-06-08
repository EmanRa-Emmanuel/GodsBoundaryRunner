Branch suggestion: feature/async-level-loading-and-safety

Summary of changes:
- Implemented cancellation-capable, async incremental level generation in `LevelManager`.
- Added `CancelLoad()` to abort background generation.
- `GameEngine` starts async load via `ResetToLevel()` and enters `GameState.Loading` while waiting.
- Loading screen with progress, spinner, and `ESC` cancel hint added in `UIOverlay.DrawLoading`.
- Added centralized `Logger`, `FilePaths` (AppData directory), and `JsonOptions` for consistent JSON serialization.
- Swapped per-frame `new Random()` instances for `Random.Shared` in systems to reduce allocations.
- Replaced continuous flight with jump+gravity in `PlayerController` for normal running behavior.
- Fixed audio buffer lifetime by allocating unmanaged buffers and freeing them on dispose.
- Replaced swallowed exceptions with logged messages to `dotnet-run.log`.

Files to review:
- Core/Logger.cs
- Core/FilePaths.cs
- Core/JsonOptions.cs
- Systems/LevelManager.cs
- Systems/GameEngine.cs
- UI/UIOverlay.cs
- Systems/SoundManager.cs
- Entities/PlayerController.cs
- Systems/FXSystem.cs
- Core/InputManager.cs, Core/SaveManager.cs, Core/SettingsManager.cs

Suggested commands to create a branch, commit, push and open a PR (GitHub):

```bash
# from repo root
git checkout -b feature/async-level-loading-and-safety
git add .
git commit -m "Add async cancellable level loading, loading UI, logging, stability fixes"
git push origin feature/async-level-loading-and-safety
# then open PR via GitHub web UI or use gh cli:
# gh pr create --fill --base main --head feature/async-level-loading-and-safety
```

Notes:
- The background generator is cooperative and will stop when cancelled. On cancellation the level remains unloaded and the UI returns to Menu.
- I preserved synchronous `LoadLevel()` for tests and legacy usage; `StartLoadLevel()` is the async entry.
- Run `dotnet build` and `dotnet test` locally to verify on your environment (I ran them successfully here).

If you want, I can create a smaller, focused PR that only contains the loading/cancellation changes and a separate PR for logging/audio fixes.
