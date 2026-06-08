using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using GodsBoundaryRunner.Core;

namespace GodsBoundaryRunner.Systems
{
    public class LevelManager
    {
        public List<Obstacle> Obstacles = new List<Obstacle>();
        public List<Ankh> Ankhs = new List<Ankh>();
        public LevelData CurrentLevel;
        public float LevelProgress = 0f;

        // Async loading state
        public bool IsLoading { get; private set; } = false;
        public float LoadProgress { get; private set; } = 0f; // 0..1
        private readonly object loadLock = new object();
        private System.Threading.CancellationTokenSource? loadCts;

        public void LoadLevel(LevelID id)
        {
            // synchronous legacy generator (keeps previous behavior)
            GenerateLevelSync(id);
        }

        public void StartLoadLevel(LevelID id)
        {
            if (IsLoading) return;
            IsLoading = true;
            LoadProgress = 0f;

            loadCts = new System.Threading.CancellationTokenSource();
            var token = loadCts.Token;

            // Run generation on background thread and assign atomically
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var (obs, ankhs, level) = await GenerateLevelBatchedAsync(id, token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                    {
                        lock (loadLock)
                        {
                            Obstacles = obs;
                            Ankhs = ankhs;
                            CurrentLevel = level;
                            LevelProgress = 0f;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // load canceled
                }
                finally
                {
                    LoadProgress = token.IsCancellationRequested ? 0f : 1f;
                    IsLoading = false;
                    loadCts = null;
                }
            }, token);
        }

        // Generate level on calling thread (existing behavior)
        private void GenerateLevelSync(LevelID id)
        {
            var task = GenerateLevelBatchedAsync(id, System.Threading.CancellationToken.None);
            var result = task.GetAwaiter().GetResult();
            Obstacles = result.Item1;
            Ankhs = result.Item2;
            CurrentLevel = result.Item3;
            LevelProgress = 0f;
        }

        // Generate level in-memory and report progress via LoadProgress field
        private async System.Threading.Tasks.Task<(List<Obstacle>, List<Ankh>, LevelData)> GenerateLevelBatchedAsync(LevelID id, System.Threading.CancellationToken token)
        {
            var ObstaclesLocal = new List<Obstacle>();
            var AnkhsLocal = new List<Ankh>();
            LevelData levelData = new LevelData();

            LoadProgress = 0f;

            switch (id)
            {
                case LevelID.Tehuti:
                    levelData = new LevelData
                    {
                        Name = "Temple of Tehuti",
                        ThemeColor = Color.Lime,
                        ID = id,
                        Description = "Level 1: Precise timing and rhythmic hurdles under Emerald light."
                    };
                    break;
                case LevelID.Ra:
                    levelData = new LevelData
                    {
                        Name = "Ascension (Temple of Ra)",
                        ThemeColor = Color.Orange,
                        ID = id,
                        Description = "Level 2: Dodging celestial beams under Solar Gold."
                    };
                    break;
                case LevelID.Geb:
                    levelData = new LevelData
                    {
                        Name = "Temple of Geb",
                        ThemeColor = new Color(255, 191, 0, 255), // Majestic Amber
                        ID = id,
                        Description = "Level 3: Rising earth obelisks and heavy dust particles."
                    };
                    break;
                case LevelID.Heru:
                    levelData = new LevelData
                    {
                        Name = "Temple of Heru",
                        ThemeColor = Color.SkyBlue,
                        ID = id,
                        Description = "Level 4: Celestial sky trails, lightning sparks, and absolute boundary escape."
                    };
                    break;
            }
            // Seed a consistent yet unique random generator for each level
            System.Random rand = new System.Random((int)id * 888 + 99);

            float x = 1200f;
            bool placedEyeOfRa = false;

            int iter = 0;
            const int yieldEvery = 12; // yield back to scheduler every N iterations
            while (x < Constants.LevelLength - 3000f)
            {
                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                // Decide which type of obstacle to spawn based on LevelID
                Obstacle obs = new Obstacle { Active = true };
                bool shouldSpawn = true;

                // Mid-point cinematic trigger check for Ra's level
                if (id == LevelID.Ra && x > 72000f && !placedEyeOfRa)
                {
                    obs.Type = ObstacleType.EyeOfRa;
                    obs.Rect = new Rectangle(x, Constants.BaseFloorY - 150, 70, 70);
                    placedEyeOfRa = true;
                    x += 1200f; // Give massive buffer space after Eye of Ra
                    ObstaclesLocal.Add(obs);
                    continue;
                }

                int roll = rand.Next(0, 100);

                if (id == LevelID.Tehuti)
                {
                    if (roll < 35) // Standard Hurdle
                    {
                        float h = rand.Next(50, 75);
                        obs.Type = ObstacleType.Standard;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY - h, 40, h);
                        SpawnJumpAnkhsLocal(AnkhsLocal, x, h);
                    }
                    else if (roll < 60) // Tunnel Slide
                    {
                        obs.Type = ObstacleType.Tunnel;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY - 120, 90, 40);
                        SpawnSlideAnkhsLocal(AnkhsLocal, x, 90);
                    }
                    else if (roll < 80) // Swinging Pendulum
                    {
                        float len = rand.Next(180, 230);
                        float spd = 2.0f + (float)rand.NextDouble() * 0.8f;
                        obs.Type = ObstacleType.Pendulum;
                        obs.Hinge = new Vector2(x + 25, Constants.BaseFloorY - 320);
                        obs.Length = len;
                        obs.SwingSpeed = spd;
                        obs.BasePhase = (float)rand.NextDouble() * MathF.PI;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY - 100, 50, 50);
                    }
                    else // Sinking Spire
                    {
                        float h = rand.Next(100, 130);
                        obs.Type = ObstacleType.SinkingSpire;
                        obs.InitialY = Constants.BaseFloorY;
                        obs.TargetY = Constants.BaseFloorY - h;
                        obs.CurrentY = Constants.BaseFloorY;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY, 50, h);
                        obs.IsTriggered = false;
                        SpawnJumpAnkhsLocal(AnkhsLocal, x, h);
                    }
                }
                else if (id == LevelID.Ra)
                {
                    if (roll < 45) // Solar Ray beam
                    {
                        obs.Type = ObstacleType.SolarRay;
                        obs.WarningTimer = 1.2f + (float)rand.NextDouble() * 0.5f;
                        obs.ActiveTimer = 0.5f + (float)rand.NextDouble() * 0.3f;
                        obs.IsFired = false;
                        obs.Rect = new Rectangle(x, 0, 50, Constants.BaseFloorY);
                        // Place warning indicators or a couple coins around it
                        AnkhsLocal.Add(new Ankh { Position = new Vector2(x - 100, Constants.BaseFloorY - 30), Collected = false });
                        AnkhsLocal.Add(new Ankh { Position = new Vector2(x + 150, Constants.BaseFloorY - 30), Collected = false });
                    }
                    else if (roll < 75) // Tunnel Slide
                    {
                        obs.Type = ObstacleType.Tunnel;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY - 120, 100, 40);
                        SpawnSlideAnkhsLocal(AnkhsLocal, x, 100);
                    }
                    else // Hurdle
                    {
                        float h = rand.Next(55, 80);
                        obs.Type = ObstacleType.Standard;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY - h, 40, h);
                        SpawnJumpAnkhsLocal(AnkhsLocal, x, h);
                    }
                }
                else if (id == LevelID.Geb)
                {
                    if (roll < 45) // Sinking / Rising Spire obelisks
                    {
                        float h = rand.Next(110, 150);
                        obs.Type = ObstacleType.SinkingSpire;
                        obs.InitialY = Constants.BaseFloorY;
                        obs.TargetY = Constants.BaseFloorY - h;
                        obs.CurrentY = Constants.BaseFloorY;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY, 60, h);
                        obs.IsTriggered = false;
                        SpawnJumpAnkhsLocal(AnkhsLocal, x, h);
                    }
                    else if (roll < 75) // Heavy slow Swinging Pendulum
                    {
                        float len = rand.Next(160, 200);
                        obs.Type = ObstacleType.Pendulum;
                        obs.Hinge = new Vector2(x + 25, Constants.BaseFloorY - 320);
                        obs.Length = len;
                        obs.SwingSpeed = 1.8f + (float)rand.NextDouble() * 0.6f;
                        obs.BasePhase = (float)rand.NextDouble() * MathF.PI;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY - 120, 60, 60);
                    }
                    else // Tunnels
                    {
                        obs.Type = ObstacleType.Tunnel;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY - 120, 90, 40);
                        SpawnSlideAnkhsLocal(AnkhsLocal, x, 90);
                    }
                }
                else // LevelID.Heru
                {
                    if (roll < 35) // Flying celestial lightning orbs
                    {
                        obs.Type = ObstacleType.FlyingOrb;
                        obs.FlySpeed = -350f - (float)rand.NextDouble() * 150f;
                        obs.SineAmp = 40f + (float)rand.NextDouble() * 50f;
                        obs.SineFreq = 3f + (float)rand.NextDouble() * 2f;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY - 120, 45, 45);
                    }
                    else if (roll < 65) // Ultra fast Pendulum swing
                    {
                        float len = rand.Next(190, 240);
                        obs.Type = ObstacleType.Pendulum;
                        obs.Hinge = new Vector2(x + 25, Constants.BaseFloorY - 320);
                        obs.Length = len;
                        obs.SwingSpeed = 3.0f + (float)rand.NextDouble() * 1.2f;
                        obs.BasePhase = (float)rand.NextDouble() * MathF.PI;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY - 100, 50, 50);
                    }
                    else if (roll < 85) // Spire obelisk
                    {
                        float h = rand.Next(120, 160);
                        obs.Type = ObstacleType.SinkingSpire;
                        obs.InitialY = Constants.BaseFloorY;
                        obs.TargetY = Constants.BaseFloorY - h;
                        obs.CurrentY = Constants.BaseFloorY;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY, 50, h);
                        obs.IsTriggered = false;
                        SpawnJumpAnkhsLocal(AnkhsLocal, x, h);
                    }
                    else // High Double Hurdles
                    {
                        float h = rand.Next(60, 85);
                        obs.Type = ObstacleType.Standard;
                        obs.Rect = new Rectangle(x, Constants.BaseFloorY - h, 50, h);
                        SpawnJumpAnkhsLocal(AnkhsLocal, x, h);
                    }
                }

                if (shouldSpawn)
                {
                    ObstaclesLocal.Add(obs);
                }

                // Advance X with randomized step gap to create professional spacing rhythm
                x += rand.Next(650, 1100);

                // Update load progress approximately based on x
                LoadProgress = Math.Clamp(x / Constants.LevelLength, 0f, 1f);

                // Cooperative yield to keep background generation chunked and responsive
                iter++;
                if ((iter & (yieldEvery - 1)) == 0)
                {
                    await System.Threading.Tasks.Task.Yield();
                    if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                }

                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            }

            // Add Checkpoint Altars at intervals of 35,000 pixels
            float checkpointInterval = 35000f;
            for (float cx = checkpointInterval; cx < Constants.LevelLength - 15000f; cx += checkpointInterval)
            {
                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                // Clean up a safe landing/starting runway around the checkpoint (600px left/right)
                ObstaclesLocal.RemoveAll(o => MathF.Abs(o.Rect.X - cx) < 600f);

                ObstaclesLocal.Add(new Obstacle
                {
                    Type = ObstacleType.Checkpoint,
                    Active = true,
                    Rect = new Rectangle(cx, Constants.BaseFloorY - 110, 35, 110),
                    IsTriggered = false
                });

                // Spawn beautiful guiding coins around the altar
                AnkhsLocal.Add(new Ankh { Position = new Vector2(cx - 150, Constants.BaseFloorY - 30f), Collected = false, FloatOffset = (cx - 150) * 0.05f });
                AnkhsLocal.Add(new Ankh { Position = new Vector2(cx - 100, Constants.BaseFloorY - 60f), Collected = false, FloatOffset = (cx - 100) * 0.05f });
                AnkhsLocal.Add(new Ankh { Position = new Vector2(cx + 100, Constants.BaseFloorY - 60f), Collected = false, FloatOffset = (cx + 100) * 0.05f });
                AnkhsLocal.Add(new Ankh { Position = new Vector2(cx + 150, Constants.BaseFloorY - 30f), Collected = false, FloatOffset = (cx + 150) * 0.05f });
            }

            // Always add a grand exit Gate at the very end of the stage
            ObstaclesLocal.Add(new Obstacle
            {
                Type = ObstacleType.Gate,
                Active = true,
                Rect = new Rectangle(Constants.LevelLength - 150, Constants.BaseFloorY - 180, 40, 180)
            });

            LoadProgress = 1f;

            if (token.IsCancellationRequested) throw new OperationCanceledException(token);

            return (ObstaclesLocal, AnkhsLocal, levelData);
        }

        // Local helpers that operate on provided lists (used by batched generation)
        private void SpawnJumpAnkhsLocal(List<Ankh> list, float obsX, float obsHeight)
        {
            float startX = obsX - 120f;
            float step = 60f;

            for (int i = 0; i < 5; i++)
            {
                float x = startX + i * step;
                float distFromCenter = (x - obsX);
                float y = Constants.BaseFloorY - (obsHeight + 60f) + (distFromCenter * distFromCenter) * 0.005f;
                y = Math.Clamp(y, 150f, Constants.BaseFloorY - 30f);

                list.Add(new Ankh
                {
                    Position = new Vector2(x, y),
                    Collected = false,
                    FloatOffset = x * 0.05f,
                    RotationAngle = 0f
                });
            }
        }

        private void SpawnSlideAnkhsLocal(List<Ankh> list, float slideX, float width)
        {
            float startX = slideX + 15f;
            float step = (width - 30f) / 2f;

            for (int i = 0; i < 3; i++)
            {
                float x = startX + i * step;
                list.Add(new Ankh
                {
                    Position = new Vector2(x, Constants.BaseFloorY - 20f),
                    Collected = false,
                    FloatOffset = x * 0.05f,
                    RotationAngle = 0f
                });
            }
        }

        public void CancelLoad()
        {
            try
            {
                loadCts?.Cancel();
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to cancel level load", ex);
            }
            finally
            {
                IsLoading = false;
                loadCts = null;
                LoadProgress = 0f;
            }
        }

        private void SpawnJumpAnkhs(float obsX, float obsHeight)
        {
            // Parabolic coin arch guiding the player beautifully over the obstacle
            float startX = obsX - 120f;
            float step = 60f;

            for (int i = 0; i < 5; i++)
            {
                float x = startX + i * step;
                // Calculate height of parabola: peak is at obsX, rising to obsHeight + 50
                float distFromCenter = (x - obsX);
                float y = Constants.BaseFloorY - (obsHeight + 60f) + (distFromCenter * distFromCenter) * 0.005f;
                
                // Keep it within reasonable bounds
                y = Math.Clamp(y, 150f, Constants.BaseFloorY - 30f);

                Ankhs.Add(new Ankh
                {
                    Position = new Vector2(x, y),
                    Collected = false,
                    FloatOffset = x * 0.05f,
                    RotationAngle = 0f
                });
            }
        }

        private void SpawnSlideAnkhs(float slideX, float width)
        {
            // Low horizontal line of 3 coins directing a slide under tunnels
            float startX = slideX + 15f;
            float step = (width - 30f) / 2f;

            for (int i = 0; i < 3; i++)
            {
                float x = startX + i * step;
                Ankhs.Add(new Ankh
                {
                    Position = new Vector2(x, Constants.BaseFloorY - 20f),
                    Collected = false,
                    FloatOffset = x * 0.05f,
                    RotationAngle = 0f
                });
            }
        }
    }
}
