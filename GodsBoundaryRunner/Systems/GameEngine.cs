using System;
using System.Numerics;
using System.Collections.Generic;
using Raylib_cs;
using GodsBoundaryRunner.Core;
using GodsBoundaryRunner.Entities;
using GodsBoundaryRunner.UI;

namespace GodsBoundaryRunner.Systems
{
    public class GameEngine
    {
        private static readonly Random rng = System.Random.Shared;
        private GameState currentState = GameState.Menu;
        private PlayerController player = null!;
        private LevelManager levelManager = null!;
        private FXSystem fxSystem = null!;
        private UIOverlay ui = null!;
        private SoundManager sound = null!;
        
        // Advanced Camera & Screenshake
        private Camera2D camera;
        // Render-to-texture for consistent internal resolution and fullscreen scaling
        private RenderTexture2D worldTarget;
        private int internalWidth = 1280;
        private int internalHeight = 720;
        private float shakeIntensity = 0f;
        private float gameTime = 0f;
        private float levelFlashOpacity = 0f;
        // Fixed timestep accumulator for stable physics
        private float physicsAccumulator = 0f;
        private const float fixedDt = 1f / 120f;
        
        private int globalAnkhCount = 0;
        private float lastCheckpointX = 200f;
        private int checkpointAnkhCount = 0;
        private float cinematicTimer = 0f;
        private string dialogueText = "";
        private bool isVictory = false;
        
        private Color skyBottomColor = Constants.SkyBottomColor;

        public void Initialize()
        {
            // Load user settings and input mappings
            SettingsManager.Load();
            GodsBoundaryRunner.Core.InputManager.Load();

            // Configure window properties: VSync, Resizable, and MSAA 4x antialiasing
            Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);

            // Initialize window. Use primary monitor resolution when user prefers fullscreen.
            if (SettingsManager.Config.StartFullscreen)
            {
                int mw = Raylib.GetMonitorWidth(0);
                int mh = Raylib.GetMonitorHeight(0);
                Raylib.InitWindow(mw, mh, "G.O.D.S: Boundary Runner (Season 1)");
                if (!Raylib.IsWindowFullscreen())
                {
                    Raylib.ToggleFullscreen();
                }
            }
            else
            {
                Raylib.InitWindow(Constants.ScreenWidth, Constants.ScreenHeight, "G.O.D.S: Boundary Runner (Season 1)");
            }

            Raylib.SetTargetFPS(60);
            // Disable Raylib's default exit key (Escape) so Escape can be used for in-game pause/remap
            Raylib.SetExitKey((KeyboardKey)0);

            // Initialize Modular Systems
            levelManager = new LevelManager();
            fxSystem = new FXSystem();
            ui = new UIOverlay();
            sound = new SoundManager();
            sound.Initialize();

            // Setup spring-damped camera
            camera = new Camera2D
            {
                Offset = new Vector2(250, 0), // Base player focus point offset
                Target = Vector2.Zero,
                Rotation = 0f,
                Zoom = 1.0f
            };
            
            // Load save data (if present) and reset into saved level/checkpoint
            GodsBoundaryRunner.Core.SaveManager.Load();
            var saved = GodsBoundaryRunner.Core.SaveManager.Data;
            ResetToLevel(saved.CurrentLevel);
            // Restore checkpoint position and ankhs
            lastCheckpointX = saved.LastCheckpointX;
            globalAnkhCount = saved.Ankhs;

            // Create a render target at a fixed internal resolution to scale to fullscreen
            worldTarget = Raylib.LoadRenderTexture(internalWidth, internalHeight);
        }

        private void ResetToLevel(LevelID id)
        {
            // Start async level load and show loading UI. Post-load setup is done when loading completes.
            levelManager.StartLoadLevel(id);
            currentState = GameState.Loading;
            // Reset transient state while loading
            lastCheckpointX = 200f;
            checkpointAnkhCount = globalAnkhCount;
            isVictory = false;
            if (id == LevelID.Tehuti)
            {
                globalAnkhCount = 0;
                checkpointAnkhCount = 0;
                lastCheckpointX = 200f;
                skyBottomColor = Constants.SkyBottomColor;
            }
        }

        private void ResetToCheckpoint()
        {
            // Reload the level to reset obstacles and Ankhs
            levelManager.LoadLevel(levelManager.CurrentLevel.ID);
            
            // Re-instantiate player at the checkpoint X coordinate
            player = new PlayerController(new Vector2(lastCheckpointX, Constants.BaseFloorY - Constants.PlayerHeight));
            
            // Set velocity to 0 to wait for input
            player.Velocity = Vector2.Zero;
            
            // Clear trail systems
            fxSystem.ClearTrail();
            
            // Reset Ankhs count to the saved checkpoint count
            globalAnkhCount = checkpointAnkhCount;
            
            // Filter out or disable any Ankhs that are already behind the player's checkpoint
            for (int i = levelManager.Ankhs.Count - 1; i >= 0; i--)
            {
                if (levelManager.Ankhs[i].Position.X < lastCheckpointX)
                {
                    levelManager.Ankhs.RemoveAt(i);
                }
            }
            
            // Mark any checkpoints that are behind or at the player's checkpoint as triggered so they don't reactivate

            for (int i = 0; i < levelManager.Obstacles.Count; i++)
            {
                var obs = levelManager.Obstacles[i];
                if (obs.Type == ObstacleType.Checkpoint && obs.Rect.X <= lastCheckpointX)
                {
                    obs.IsTriggered = true;
                    levelManager.Obstacles[i] = obs;
                }
            }
            
            // Banner to welcome player back
            ui.StartLevelBanner("RUN RESTORED", $"Re-materializing at Sector coordinate {(int)lastCheckpointX}m.");
            sound.PlayGate();
            levelFlashOpacity = 0.6f;

        }

        public void Run()
        {
            while (!Raylib.WindowShouldClose())
            {
                float dt = Raylib.GetFrameTime();
                Update(dt);
                Draw();
            }
            sound.Dispose();
            // Unload render target
            try { Raylib.UnloadRenderTexture(worldTarget); } catch { }
            Raylib.CloseWindow();
        }

        private void Update(float dt)
        {
            // Toggle fullscreen with F11
            if (Raylib.IsKeyPressed(KeyboardKey.F11))
            {
                Raylib.ToggleFullscreen();
            }

            gameTime += dt;
            ui.Update(dt);
            DebugTools.Update();

            // Decay flash screen overlays and screenshake
            if (levelFlashOpacity > 0f) levelFlashOpacity -= 2.5f * dt;
            if (shakeIntensity > 0f) shakeIntensity = Raymath.Lerp(shakeIntensity, 0f, 8f * dt);

            switch (currentState)
            {
                case GameState.Menu:
                    if (Raylib.IsKeyPressed(KeyboardKey.Space))
                    {
                        // Start from menu -> load first level
                        ResetToLevel(LevelID.Tehuti);
                        sound.PlayJump();
                    }
                    break;

                case GameState.Loading:
                    // Allow canceling load with Escape
                    if (Raylib.IsKeyPressed(KeyboardKey.Escape))
                    {
                        levelManager.CancelLoad();
                        currentState = GameState.Menu;
                        break;
                    }

                    // Show loading screen until level data is ready
                    if (!levelManager.IsLoading)
                    {
                        // Finalize post-load setup
                        player = new PlayerController(new Vector2(200, Constants.BaseFloorY - Constants.PlayerHeight));
                        fxSystem.ClearTrail();
                        isVictory = false;

                        // Banner for level start
                        ui.StartLevelBanner(levelManager.CurrentLevel.Name, levelManager.CurrentLevel.Description);
                        sound.PlayGate();
                        levelFlashOpacity = 0.8f;

                        currentState = GameState.Playing;
                    }
                    break;

                case GameState.Playing:
                    // Capture input triggers for procedural audio
                    bool pressJump = Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.Space);
                    bool pressDown = Raylib.IsKeyPressed(KeyboardKey.S) || Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.LeftControl);

                    if (pressJump && player.IsGrounded && !player.IsSliding && player.StumbleTimer <= 0)
                    {
                        sound.PlayJump();
                    }
                    if (pressDown && player.IsGrounded && !player.IsSliding && player.StumbleTimer <= 0)
                    {
                        bool isMoving = MathF.Abs(player.Velocity.X) > 50f || Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right) || Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left);
                        if (isMoving)
                        {
                            sound.PlaySlide();
                        }
                    }

                        // Pause toggle
                        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
                        {
                            currentState = GameState.Paused;
                        }

                    // Fixed timestep physics loop for stable collisions and gravity
                    physicsAccumulator += dt;
                    if (physicsAccumulator > 0.25f) physicsAccumulator = 0.25f; // avoid spiral of death

                    while (physicsAccumulator >= fixedDt)
                    {
                        player.Update(fixedDt);
                        UpdateObstacles(fixedDt);
                        UpdateAnkhs(fixedDt);
                        HandleCollisions();
                        fxSystem.Update(fixedDt, player.Position);
                        physicsAccumulator -= fixedDt;
                    }

                    // Derived visual state
                    levelManager.LevelProgress = player.Position.X - 200f;

                    // Spring-damped tracking camera target update
                    float targetCamX = player.Position.X;
                    camera.Target.X = Raymath.Lerp(camera.Target.X, targetCamX, 6f * dt);
                    camera.Target.Y = 0f;

                    // Speed Zoom: zoom out as velocity increases to heighten the speed rush
                    float speedRatio = Math.Clamp(MathF.Abs(player.Velocity.X) / Constants.MaxRunSpeed, 0f, 1f);
                    camera.Zoom = Raymath.Lerp(camera.Zoom, 1.0f - speedRatio * 0.12f, 4f * dt);
                    
                    if (levelManager.LevelProgress >= Constants.LevelLength)
                    {
                        TransitionLevel();
                    }
                    break;

                case GameState.Cinematic:
                    cinematicTimer -= dt;
                    
                    // Force camera close-up on contact with Eye of Ra
                    camera.Target.X = Raymath.Lerp(camera.Target.X, player.Position.X, 4f * dt);
                    camera.Zoom = Raymath.Lerp(camera.Zoom, 1.25f, 3f * dt);

                    if (cinematicTimer <= 0)
                    {
                        currentState = GameState.Playing;
                        skyBottomColor = Constants.NightSkyBottomColor;
                    }
                    break;

                case GameState.Paused:
                    // Adjust gravity multiplier with Left/Right and speed multiplier with Up/Down
                    if (Raylib.IsKeyPressed(KeyboardKey.Escape))
                    {
                        currentState = GameState.Playing;
                    }

                    if (Raylib.IsKeyPressed(KeyboardKey.Left))
                    {
                        SettingsManager.Config.GravityMultiplier = MathF.Max(0.2f, SettingsManager.Config.GravityMultiplier - 0.05f);
                        SettingsManager.Save();
                    }
                    if (Raylib.IsKeyPressed(KeyboardKey.Right))
                    {
                        SettingsManager.Config.GravityMultiplier = MathF.Min(4f, SettingsManager.Config.GravityMultiplier + 0.05f);
                        SettingsManager.Save();
                    }
                    if (Raylib.IsKeyPressed(KeyboardKey.Up))
                    {
                        SettingsManager.Config.MaxRunSpeedMultiplier = MathF.Min(2.5f, SettingsManager.Config.MaxRunSpeedMultiplier + 0.05f);
                        SettingsManager.Save();
                    }
                    if (Raylib.IsKeyPressed(KeyboardKey.Down))
                    {
                        SettingsManager.Config.MaxRunSpeedMultiplier = MathF.Max(0.5f, SettingsManager.Config.MaxRunSpeedMultiplier - 0.05f);
                        SettingsManager.Save();
                    }
                    // Mouse click to start remapping actions in the pause menu
                    if (Raylib.IsMouseButtonPressed(0))
                    {
                        var mp = Raylib.GetMousePosition();
                        var action = ui.GetActionAtPoint(mp);
                        if (!string.IsNullOrEmpty(action))
                        {
                            ui.StartRemap(action);
                        }
                    }

                    // If UI is in remapping mode, capture next key or gamepad button
                    if (ui.IsRemapping)
                    {
                        // Cancel with Escape
                        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
                        {
                            ui.CancelRemap();
                        }
                        else
                        {
                            // Check keyboard keys
                            foreach (KeyboardKey k in Enum.GetValues(typeof(KeyboardKey)))
                            {
                                if (Raylib.IsKeyPressed(k))
                                {
                                    InputManager.SetMapping(ui.RemapAction, new List<KeyboardKey> { k }, new List<GamepadButton>());
                                    ui.CancelRemap();
                                    break;
                                }
                            }

                            // Check gamepad buttons across available gamepads
                            if (ui.IsRemapping)
                            {
                                for (int gid = 0; gid < 4 && ui.IsRemapping; gid++)
                                {
                                    if (!Raylib.IsGamepadAvailable(gid)) continue;
                                    foreach (GamepadButton b in Enum.GetValues(typeof(GamepadButton)))
                                    {
                                        if (Raylib.IsGamepadButtonPressed(gid, b))
                                        {
                                            InputManager.SetMapping(ui.RemapAction, new List<KeyboardKey>(), new List<GamepadButton> { b });
                                            ui.CancelRemap();
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case GameState.GameOver:
                    if (Raylib.IsKeyPressed(KeyboardKey.R))
                    {
                        if (isVictory)
                        {
                            ResetToLevel(LevelID.Tehuti);
                        }
                        else
                        {
                            ResetToCheckpoint();
                        }
                        currentState = GameState.Playing;
                    }
                    break;
            }
        }

        private void UpdateObstacles(float dt)
        {
            for (int i = 0; i < levelManager.Obstacles.Count; i++)
            {
                var obs = levelManager.Obstacles[i];
                if (!obs.Active) continue;

                switch (obs.Type)
                {
                    case ObstacleType.Pendulum:
                        // Clockwork swing mechanics
                        obs.SwingAngle = MathF.Sin(gameTime * obs.SwingSpeed + obs.BasePhase) * 1.0f; // Max swing angle
                        
                        // Recalculate collision box of bob in world space coordinates
                        float bobX = obs.Hinge.X + MathF.Sin(obs.SwingAngle) * obs.Length;
                        float bobY = obs.Hinge.Y + MathF.Cos(obs.SwingAngle) * obs.Length;
                        
                        obs.Rect.X = bobX - obs.Rect.Width / 2f;
                        obs.Rect.Y = bobY - obs.Rect.Height / 2f;
                        break;

                    case ObstacleType.SinkingSpire:
                        // Check if player is approaching spire
                        float distToSpire = obs.Rect.X - player.Position.X;
                        if (distToSpire < 450f)
                        {
                            obs.IsTriggered = true;
                        }

                        if (obs.IsTriggered)
                        {
                            // Smoothly rise up from below the floor
                            obs.CurrentY = Raymath.Lerp(obs.CurrentY, obs.TargetY, 5f * dt);
                        }
                        else
                        {
                            obs.CurrentY = obs.InitialY;
                        }
                        obs.Rect.Y = obs.CurrentY;
                        break;

                    case ObstacleType.SolarRay:
                        // Cycle through flashing warn phase and dynamic active laser firing
                        if (obs.WarningTimer > 0)
                        {
                            obs.WarningTimer -= dt;
                        }
                        else
                        {
                            obs.IsFired = true;
                            if (obs.ActiveTimer > 0)
                            {
                                obs.ActiveTimer -= dt;
                            }
                            else
                            {
                                // Recharge cycle
                                obs.WarningTimer = 1.8f;
                                obs.ActiveTimer = 0.5f;
                                obs.IsFired = false;
                            }
                        }
                        break;

                    case ObstacleType.FlyingOrb:
                        // Float leftwards while weaving on a dynamic sine wave
                        obs.Rect.X += obs.FlySpeed * dt;
                        float initialY = Constants.BaseFloorY - 120f;
                        obs.Rect.Y = initialY + MathF.Sin(gameTime * obs.SineFreq) * obs.SineAmp;
                        break;
                }

                levelManager.Obstacles[i] = obs;
            }
        }

        private void UpdateAnkhs(float dt)
        {
            for (int i = 0; i < levelManager.Ankhs.Count; i++)
            {
                var ankh = levelManager.Ankhs[i];
                ankh.RotationAngle += 150f * dt;
                levelManager.Ankhs[i] = ankh;
            }
        }

        private void HandleCollisions()
        {
            Rectangle playerBounds = player.GetBounds();

            // Obstacle Collisions
            for (int i = 0; i < levelManager.Obstacles.Count; i++)
            {
                var obs = levelManager.Obstacles[i];
                if (!obs.Active) continue;

                // Solar ray checks have unique infinite height intersection bounds
                if (obs.Type == ObstacleType.SolarRay)
                {
                    if (obs.IsFired)
                    {
                        Rectangle rayRect = new Rectangle(obs.Rect.X, 0, obs.Rect.Width, Constants.BaseFloorY);
                        
                        if (Raylib.CheckCollisionRecs(playerBounds, rayRect))
                        {
                            TriggerCrash();
                        }
                    }
                    continue;
                }

                // Standard and dynamic obstacle bounds projections
                Rectangle worldRect = obs.Rect;

                if (Raylib.CheckCollisionRecs(playerBounds, worldRect))
                {
                    if (obs.Type == ObstacleType.EyeOfRa)
                    {
                        currentState = GameState.Cinematic;
                        cinematicTimer = 4.0f;
                        dialogueText = "\"THE JOURNEY IS ONLY BEGINNING.\"";
                        obs.Active = false;
                        levelManager.Obstacles[i] = obs;
                        player.Velocity.X = 0;
                        shakeIntensity = 5f;
                    }
                    else if (obs.Type == ObstacleType.Gate)
                    {
                        TransitionLevel();
                    }
                    else if (obs.Type == ObstacleType.Checkpoint)
                    {
                        if (!obs.IsTriggered)
                        {
                            obs.IsTriggered = true;
                            levelManager.Obstacles[i] = obs; // Save state

                            lastCheckpointX = obs.Rect.X;
                            checkpointAnkhCount = globalAnkhCount;

                            sound.PlayGate();
                            fxSystem.SpawnBurst(new Vector2(obs.Rect.X + 17f, obs.Rect.Y + 40f), levelManager.CurrentLevel.ThemeColor, 35);
                            fxSystem.SpawnBurst(new Vector2(obs.Rect.X + 17f, obs.Rect.Y + 40f), Color.Gold, 15);
                            
                            ui.StartLevelBanner("CHECKPOINT ACTIVATED", "Sacred boundary anchor synchronized. Progress saved.");
                            levelFlashOpacity = 0.5f; // Neon energy burst flash!
                            // Persist save data
                            GodsBoundaryRunner.Core.SaveManager.Data.LastCheckpointX = lastCheckpointX;
                            GodsBoundaryRunner.Core.SaveManager.Data.Ankhs = globalAnkhCount;
                            GodsBoundaryRunner.Core.SaveManager.Data.CurrentLevel = levelManager.CurrentLevel.ID;
                            GodsBoundaryRunner.Core.SaveManager.Save();
                        }
                    }
                    else
                    {
                        TriggerCrash();
                    }
                }
            }

            // Ankh Collisions
            for (int i = levelManager.Ankhs.Count - 1; i >= 0; i--)
            {
                var ankh = levelManager.Ankhs[i];
                // Floating vertical bounce height
                float animatedY = ankh.Position.Y + MathF.Sin(gameTime * 4f + ankh.FloatOffset) * 10f;

                if (Raylib.CheckCollisionCircleRec(new Vector2(ankh.Position.X, animatedY), 12f, playerBounds))
                {
                    globalAnkhCount++;
                    sound.PlayCollect();
                    fxSystem.SpawnBurst(new Vector2(ankh.Position.X, animatedY), Color.Gold, 6);
                    levelManager.Ankhs.RemoveAt(i);
                }
            }
        }

        private void TriggerCrash()
        {
            currentState = GameState.GameOver;
            isVictory = false;
            sound.PlayStumble();
            shakeIntensity = 25f; // Stronger impact screenshake
            fxSystem.SpawnBurst(player.Position + new Vector2(Constants.PlayerWidth / 2f, Constants.PlayerHeight / 2f), Color.Red, 35);
        }

        private void TransitionLevel()
        {
            int nextLevel = (int)levelManager.CurrentLevel.ID + 1;
            if (nextLevel > (int)LevelID.Heru)
            {
                currentState = GameState.GameOver;
                isVictory = true;
            }
            else
            {
                ResetToLevel((LevelID)nextLevel);
            }
        }

        private void Draw()
        {
            // Render world to an internal-sized render target then scale to screen to improve fullscreen performance
            Raylib.BeginTextureMode(worldTarget);
            {
                Raylib.ClearBackground(Color.Black);

                // 1. SKYBOX LAYER (static backdrop gradient)
                Raylib.DrawRectangleGradientV(0, 0, internalWidth, (int)(Constants.BaseFloorY * (internalHeight / (float)Constants.ScreenHeight)), Constants.SkyTopColor, skyBottomColor);

            // Apply screenshake directly to camera structure offset
            Vector2 baseOffset = new Vector2(250, 0);
                if (shakeIntensity > 0)
                {
                    float shakeX = (float)(rng.NextDouble() * 2.0 - 1.0) * shakeIntensity;
                    float shakeY = (float)(rng.NextDouble() * 2.0 - 1.0) * shakeIntensity;
                    camera.Offset = baseOffset + new Vector2(shakeX, shakeY);
                }
            else
            {
                camera.Offset = baseOffset;
            }

            // 2. PARALLAX BACKDROP LAYERS (Behind the camera)
            DrawParallaxBackdrops();

            // 3. CAM LAYER (World objects and player)
            Raylib.BeginMode2D(camera);
            {
                // Dynamic grid lines on floor to increase speed visualization
                DrawFloorGrid();

                // Draw Ankhs with rotating neon outer rings
                foreach (var ankh in levelManager.Ankhs)
                {
                    float animatedY = ankh.Position.Y + MathF.Sin(gameTime * 4f + ankh.FloatOffset) * 10f;
                    
                    // Draw neon outer orbiting rings
                    float rad = 10f;
                    FXSystem.DrawNeonCircle(new Vector2(ankh.Position.X, animatedY), rad + 3f, levelManager.CurrentLevel.ThemeColor);
                    Raylib.DrawCircle((int)ankh.Position.X, (int)animatedY, rad, Color.Gold);
                    
                    // Sacred cross design primitive inside gold coin
                    Raylib.DrawLine((int)ankh.Position.X, (int)animatedY - 6, (int)ankh.Position.X, (int)animatedY + 6, Color.Black);
                    Raylib.DrawLine((int)ankh.Position.X - 4, (int)animatedY - 2, (int)ankh.Position.X + 4, (int)animatedY - 2, Color.Black);
                }

                // Draw Obstacles (with glowing neon highlights)
                foreach (var obs in levelManager.Obstacles)
                {
                    if (!obs.Active) continue;

                    switch (obs.Type)
                    {
                        case ObstacleType.Standard:
                        case ObstacleType.Tunnel:
                            // Draw obsidian block outline with neon bloom borders
                            Raylib.DrawRectangleRec(obs.Rect, Color.Black);
                            FXSystem.DrawNeonLine(new Vector2(obs.Rect.X, obs.Rect.Y), new Vector2(obs.Rect.X + obs.Rect.Width, obs.Rect.Y), 2f, levelManager.CurrentLevel.ThemeColor);
                            FXSystem.DrawNeonLine(new Vector2(obs.Rect.X, obs.Rect.Y), new Vector2(obs.Rect.X, obs.Rect.Y + obs.Rect.Height), 1.5f, levelManager.CurrentLevel.ThemeColor);
                            FXSystem.DrawNeonLine(new Vector2(obs.Rect.X + obs.Rect.Width, obs.Rect.Y), new Vector2(obs.Rect.X + obs.Rect.Width, obs.Rect.Y + obs.Rect.Height), 1.5f, levelManager.CurrentLevel.ThemeColor);
                            break;

                        case ObstacleType.Pendulum:
                            // Draw glowing neon wire hanging cord
                            FXSystem.DrawNeonLine(obs.Hinge, obs.Rect.Position + new Vector2(obs.Rect.Width / 2f, obs.Rect.Height / 2f), 1.5f, levelManager.CurrentLevel.ThemeColor);
                            
                            // Bob ball
                            Raylib.DrawCircleV(obs.Rect.Position + new Vector2(obs.Rect.Width / 2f, obs.Rect.Height / 2f), obs.Rect.Width / 2f, Color.Black);
                            FXSystem.DrawNeonCircle(obs.Rect.Position + new Vector2(obs.Rect.Width / 2f, obs.Rect.Height / 2f), obs.Rect.Width / 2f, levelManager.CurrentLevel.ThemeColor, true);
                            break;

                        case ObstacleType.SinkingSpire:
                            // Draw rising heavy obsidian obelisk
                            Raylib.DrawRectangleRec(obs.Rect, Color.Black);
                            FXSystem.DrawNeonLine(new Vector2(obs.Rect.X, obs.Rect.Y), new Vector2(obs.Rect.X + obs.Rect.Width, obs.Rect.Y), 2.5f, levelManager.CurrentLevel.ThemeColor);
                            
                            // Vertical neon accent strip down the middle
                            FXSystem.DrawNeonLine(new Vector2(obs.Rect.X + obs.Rect.Width / 2f, obs.Rect.Y), new Vector2(obs.Rect.X + obs.Rect.Width / 2f, obs.Rect.Y + obs.Rect.Height), 1f, levelManager.CurrentLevel.ThemeColor);
                            break;

                        case ObstacleType.SolarRay:
                            // Draw charging warn sequence or active fired death lasers
                            if (obs.IsFired)
                            {
                                Color glowCol = Color.Orange; glowCol.A = 40;
                                Raylib.DrawRectangle((int)obs.Rect.X - 10, 0, (int)obs.Rect.Width + 20, (int)Constants.BaseFloorY, glowCol);
                                
                                // Core high-intensity laser
                                FXSystem.DrawNeonLine(new Vector2(obs.Rect.X + obs.Rect.Width / 2f, 0), new Vector2(obs.Rect.X + obs.Rect.Width / 2f, Constants.BaseFloorY), 8f, Color.Gold);
                            }
                            else
                            {
                                // Warning Telegraph line (flickers)
                                float flickerAlpha = 0.2f + MathF.Sin(gameTime * 45f) * 0.15f;
                                Color warnColor = Color.Orange;
                                warnColor.A = (byte)(flickerAlpha * 255);
                                Raylib.DrawLineEx(new Vector2(obs.Rect.X + obs.Rect.Width / 2f, 0), new Vector2(obs.Rect.X + obs.Rect.Width / 2f, Constants.BaseFloorY), 2f, warnColor);
                            }
                            break;

                        case ObstacleType.FlyingOrb:
                            // Flying glowing sphere
                            Vector2 orbCenter = obs.Rect.Position + new Vector2(obs.Rect.Width / 2f, obs.Rect.Height / 2f);
                            Raylib.DrawCircleV(orbCenter, obs.Rect.Width / 2f, Color.Black);
                            FXSystem.DrawNeonCircle(orbCenter, obs.Rect.Width / 2f, levelManager.CurrentLevel.ThemeColor, true);
                            break;

                        case ObstacleType.EyeOfRa:
                            // Glowing ancient sacred circle
                            Vector2 eyeCenter = obs.Rect.Position + new Vector2(obs.Rect.Width / 2f, obs.Rect.Height / 2f);
                            
                            // Concentric eye design
                            Raylib.DrawCircleV(eyeCenter, 30f, Color.Black);
                            FXSystem.DrawNeonCircle(eyeCenter, 30f, Color.Orange, true);
                            FXSystem.DrawNeonCircle(eyeCenter, 15f, Color.Gold, false);
                            
                            // Iris highlight
                            float eyePulse = 5f + MathF.Sin(gameTime * 10f) * 2f;
                            Raylib.DrawCircleV(eyeCenter, eyePulse, Color.Black);
                            break;

                        case ObstacleType.Gate:
                            // Draw magnificent neon arch portal gate
                            Raylib.DrawRectangleRec(obs.Rect, Color.Black);
                            FXSystem.DrawNeonLine(new Vector2(obs.Rect.X, obs.Rect.Y), new Vector2(obs.Rect.X + obs.Rect.Width, obs.Rect.Y), 3f, Color.White);
                            FXSystem.DrawNeonLine(new Vector2(obs.Rect.X, obs.Rect.Y), new Vector2(obs.Rect.X, obs.Rect.Y + obs.Rect.Height), 2f, Color.White);
                            FXSystem.DrawNeonLine(new Vector2(obs.Rect.X + obs.Rect.Width, obs.Rect.Y), new Vector2(obs.Rect.X + obs.Rect.Width, obs.Rect.Y + obs.Rect.Height), 2f, Color.White);
                            
                            // Draw gate vortex particles inside the gate portal
                            float portalFlicker = 0.5f + MathF.Sin(gameTime * 20f) * 0.2f;
                            Color vortexCol = levelManager.CurrentLevel.ThemeColor;
                            vortexCol.A = (byte)(portalFlicker * 100);
                            Raylib.DrawRectangleRec(new Rectangle(obs.Rect.X + 2, obs.Rect.Y + 2, obs.Rect.Width - 4, obs.Rect.Height - 4), vortexCol);
                            break;
                    }
                }

                // Draw motion trails and particle systems
                fxSystem.Draw(levelManager.CurrentLevel.ThemeColor);

                // Draw solid black ground plane silhouettes
                Raylib.DrawRectangle((int)(camera.Target.X - 300), (int)Constants.BaseFloorY, Constants.ScreenWidth + 600, 200, Color.Black);
                Raylib.DrawLineEx(new Vector2(camera.Target.X - 300, Constants.BaseFloorY), new Vector2(camera.Target.X + Constants.ScreenWidth + 300, Constants.BaseFloorY), 3f, levelManager.CurrentLevel.ThemeColor);

                // Draw dynamic jointed procedural runner silhouette
                player.Draw(levelManager.CurrentLevel.ThemeColor);
            }
            }
            Raylib.EndTextureMode();

            // Draw the scaled render target to the screen
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            // Source rect: flip Y because RenderTexture is rendered upside-down
            Rectangle src = new Rectangle(0, 0, worldTarget.Texture.Width, -worldTarget.Texture.Height);

            // Preserve internal aspect ratio and center the scaled image on screen dynamically
            float screenW = Raylib.GetScreenWidth();
            float screenH = Raylib.GetScreenHeight();
            float scale = MathF.Min(screenW / (float)internalWidth, screenH / (float)internalHeight);
            float destW = internalWidth * scale;
            float destH = internalHeight * scale;
            float destX = (screenW - destW) * 0.5f;
            float destY = (screenH - destH) * 0.5f;

            Rectangle dest = new Rectangle(destX, destY, destW, destH);
            Raylib.DrawTexturePro(worldTarget.Texture, src, dest, new Vector2(0, 0), 0f, Color.White);

            // Draw UI in actual screen coordinates (keeps HUD crisp)
            ui.DrawHUD(levelManager.CurrentLevel, globalAnkhCount, levelManager.LevelProgress, Constants.MaxRunSpeed, player?.Velocity.X ?? 0f);

            if (currentState == GameState.Menu) ui.DrawMenu(levelManager.CurrentLevel.ThemeColor);
            if (currentState == GameState.Cinematic) ui.DrawDialogue(dialogueText, cinematicTimer);
            if (currentState == GameState.GameOver) ui.DrawGameOver(globalAnkhCount, isVictory, levelManager.CurrentLevel.Name, levelManager.LevelProgress);
            if (currentState == GameState.Paused) ui.DrawPauseMenu();
            if (currentState == GameState.Loading) ui.DrawLoading(levelManager.LoadProgress);

            // Atmospheric weather particles overlay (UI-scale)
            fxSystem.DrawAmbientEmbers(levelManager.CurrentLevel.ThemeColor);

            // Simple post-processing vignette (as UI overlay)
            fxSystem.DrawVignette(0.45f);

            // Screen flash overlay on level load
            if (levelFlashOpacity > 0f)
            {
                Color flashColor = Color.White;
                flashColor.A = (byte)(levelFlashOpacity * 255);
                Raylib.DrawRectangle(0, 0, Constants.ScreenWidth, Constants.ScreenHeight, flashColor);
            }

            Raylib.EndDrawing();

            // Draw debug overlay on top
            DebugTools.Draw();
        }

        private void DrawFloorGrid()
        {
            float gridStep = 60f;
            float offset = levelManager.LevelProgress % gridStep;
            
            Color gridColor = levelManager.CurrentLevel.ThemeColor;
            gridColor.A = 35; // Faint glow

            float startX = camera.Target.X - 300;
            float endX = camera.Target.X + Constants.ScreenWidth + 300;

            for (float x = startX - offset; x < endX; x += gridStep)
            {
                Raylib.DrawLineEx(new Vector2(x, Constants.BaseFloorY), new Vector2(x - 50, Constants.BaseFloorY + 140), 1f, gridColor);
            }

            // Horizontal receding perspective grid lines
            for (float y = Constants.BaseFloorY + 15; y < Constants.ScreenHeight; y += 30)
            {
                Raylib.DrawLineEx(new Vector2(startX, y), new Vector2(endX, y), 1f, gridColor);
            }
        }

        private void DrawParallaxBackdrops()
        {
            float progress = levelManager.LevelProgress;

            // Far Background (Temple disks / Moon / Sun silhouettes)
            if (levelManager.CurrentLevel.ID == LevelID.Ra)
            {
                // Massive solar disk backdrop
                Raylib.DrawCircle(Constants.ScreenWidth / 2, 220, 95, Color.Gold);
                Color glow = Color.Orange; glow.A = 50;
                Raylib.DrawCircle(Constants.ScreenWidth / 2, 220, 120, glow);
                
                // Throne architecture structure silhouette
                Raylib.DrawRectangle(Constants.ScreenWidth / 2 - 40, (int)Constants.BaseFloorY - 140, 80, 140, new Color(15, 12, 16, 255));
            }
            else if (levelManager.CurrentLevel.ID == LevelID.Tehuti)
            {
                // Sacred Emerald Eye of Tehuti ring in sky
                FXSystem.DrawNeonCircle(new Vector2(Constants.ScreenWidth / 2, 200), 80f, Color.Lime, false);
                FXSystem.DrawNeonCircle(new Vector2(Constants.ScreenWidth / 2, 200), 50f, Color.Lime, false);
            }
            else if (levelManager.CurrentLevel.ID == LevelID.Geb)
            {
                // Sinking planet backdrop
                Raylib.DrawCircle(Constants.ScreenWidth / 3, 250, 70, new Color(60, 45, 30, 255));
                FXSystem.DrawNeonCircle(new Vector2(Constants.ScreenWidth / 3, 250), 73f, levelManager.CurrentLevel.ThemeColor, false);
            }
            else if (levelManager.CurrentLevel.ID == LevelID.Heru)
            {
                // Electric storm sky disk
                FXSystem.DrawNeonCircle(new Vector2(Constants.ScreenWidth / 2, 180), 90f, Color.SkyBlue, false);
                FXSystem.DrawNeonCircle(new Vector2(Constants.ScreenWidth / 2, 180), 30f, Color.Gold, true);
            }

            // Layer 1: Far silhouettes (Mountains / Pyramids) - moves at 4% player speed
            float x1 = -(progress * 0.04f) % (Constants.ScreenWidth * 2f);
            DrawPyramidBackdrop(x1, 180, new Color(16, 17, 24, 255));
            DrawPyramidBackdrop(x1 + Constants.ScreenWidth, 200, new Color(16, 17, 24, 255));

            // Layer 2: Mid silhouettes (Temple Ruins / Pillars) - moves at 12% player speed
            float x2 = -(progress * 0.12f) % (Constants.ScreenWidth * 1.5f);
            DrawMidGroundRuins(x2, new Color(24, 25, 32, 255));
            DrawMidGroundRuins(x2 + Constants.ScreenWidth * 0.75f, new Color(24, 25, 32, 255));

            // Layer 3: Near silhouettes (Colonnades with neon highlight strips) - moves at 24% player speed
            float x3 = -(progress * 0.24f) % (Constants.ScreenWidth * 1.2f);
            DrawNearGroundPillars(x3, new Color(30, 31, 38, 255), levelManager.CurrentLevel.ThemeColor);
            DrawNearGroundPillars(x3 + Constants.ScreenWidth * 0.6f, new Color(30, 31, 38, 255), levelManager.CurrentLevel.ThemeColor);
        }

        private void DrawPyramidBackdrop(float scrollX, float height, Color col)
        {
            Vector2 p1 = new Vector2(scrollX, Constants.BaseFloorY);
            Vector2 p2 = new Vector2(scrollX + 200, Constants.BaseFloorY - height);
            Vector2 p3 = new Vector2(scrollX + 400, Constants.BaseFloorY);
            Raylib.DrawTriangle(p1, p3, p2, col);

            Vector2 p4 = new Vector2(scrollX + 300, Constants.BaseFloorY);
            Vector2 p5 = new Vector2(scrollX + 450, Constants.BaseFloorY - height * 0.7f);
            Vector2 p6 = new Vector2(scrollX + 600, Constants.BaseFloorY);
            Raylib.DrawTriangle(p4, p6, p5, col);
        }

        private void DrawMidGroundRuins(float scrollX, Color col)
        {
            // Giant arch portal silhouette
            Raylib.DrawRectangle((int)scrollX, (int)Constants.BaseFloorY - 180, 100, 180, col);
            Raylib.DrawRectangle((int)scrollX + 100, (int)Constants.BaseFloorY - 180, 180, 50, col);
            Raylib.DrawRectangle((int)scrollX + 280, (int)Constants.BaseFloorY - 180, 100, 180, col);

            // Single standing obelisk spire
            Vector2 top = new Vector2(scrollX + 500, Constants.BaseFloorY - 240);
            Vector2 bLeft = new Vector2(scrollX + 470, Constants.BaseFloorY);
            Vector2 bRight = new Vector2(scrollX + 530, Constants.BaseFloorY);
            Raylib.DrawTriangle(bLeft, bRight, top, col);
        }

        private void DrawNearGroundPillars(float scrollX, Color col, Color themeColor)
        {
            // Receding neon highlighted pillars
            for (int i = 0; i < 4; i++)
            {
                float x = scrollX + i * 280;
                
                // Pillar body
                Raylib.DrawRectangle((int)x, (int)Constants.BaseFloorY - 260, 45, 260, col);
                // Top capital plate
                Raylib.DrawRectangle((int)x - 10, (int)Constants.BaseFloorY - 270, 65, 10, col);

                // Thin neon glowing highlight lines down the side of pillars
                Color glowCol = themeColor;
                glowCol.A = 80;
                Raylib.DrawLineEx(new Vector2(x + 22, Constants.BaseFloorY - 250), new Vector2(x + 22, Constants.BaseFloorY - 10), 2f, glowCol);
            }
        }
    }
}
