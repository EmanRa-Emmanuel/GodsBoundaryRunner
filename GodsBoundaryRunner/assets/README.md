Run sprite-sheet format

Place a horizontal sprite-sheet PNG at: `assets/run_spritesheet.png`.

Requirements:
- Frames laid out left-to-right in equal width frames.
- Default frame count is 8. Create a file `assets/run_spritesheet.meta` containing a single number (e.g. `6`) to override frame count if needed.
- The sprite sheet will be scaled so the frame height matches the player's logical `Height`.

How to produce from your reference video:
1. Extract frames (example using ffmpeg):
   ffmpeg -i reference.mp4 -vf "scale=iw:-1" -r 24 frames/frame_%03d.png
2. Assemble into a horizontal sprite sheet (example using ImageMagick):
   magick convert frames/frame_*.png +append run_spritesheet.png
3. Copy `run_spritesheet.png` into the project's `assets` folder and optionally create `run_spritesheet.meta` with the frame count.

Notes:
- The loader runs at `GameEngine.Initialize` time so the sprite must be present before running the game.
- If the sprite is missing the engine falls back to the procedural runner.