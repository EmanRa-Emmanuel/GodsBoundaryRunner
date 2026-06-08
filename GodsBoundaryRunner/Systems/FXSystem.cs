using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using GodsBoundaryRunner.Core;

namespace GodsBoundaryRunner.Systems
{
    public class FXSystem
    {
        private struct Particle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
        }

        private struct AmbientEmber
        {
            public Vector2 Position;
            public float Speed;
            public float SwaySpeed;
            public float SwayAmount;
            public float Size;
            public float Phase;
        }

        private List<Particle> particles = new List<Particle>();
        private List<Vector2> trailPositions = new List<Vector2>();
        private List<AmbientEmber> embers = new List<AmbientEmber>();
        private static readonly Random rng = System.Random.Shared;

        public FXSystem()
        {
            // Pre-populate ambient embers
            for (int i = 0; i < 60; i++)
            {
                embers.Add(new AmbientEmber
                {
                    Position = new Vector2(rng.Next(0, Constants.ScreenWidth), rng.Next(0, (int)Constants.BaseFloorY)),
                    Speed = 30f + (float)rng.NextDouble() * 50f,
                    SwaySpeed = 1f + (float)rng.NextDouble() * 2f,
                    SwayAmount = 10f + (float)rng.NextDouble() * 30f,
                    Size = 1.5f + (float)rng.NextDouble() * 3.5f,
                    Phase = (float)rng.NextDouble() * MathF.PI * 2f
                });
            }
        }

        public void Update(float dt, Vector2 playerPos)
        {
            // Update explosive particles
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Position += p.Velocity * dt;
                
                // Add minor gravity/friction
                p.Velocity.Y += 80f * dt; 
                p.Velocity.X *= 0.96f;
                
                p.Life -= dt;
                particles[i] = p;

                if (p.Life <= 0) particles.RemoveAt(i);
            }

            // Update running motion trail
            trailPositions.Add(playerPos);
            if (trailPositions.Count > 18) trailPositions.RemoveAt(0);

            // Update atmospheric background embers
            for (int i = 0; i < embers.Count; i++)
            {
                var e = embers[i];
                e.Position.Y -= e.Speed * dt;
                e.Phase += e.SwaySpeed * dt;
                
                // Add horizontal sway
                e.Position.X += MathF.Sin(e.Phase) * e.SwayAmount * dt;

                // Reset when floating off-screen
                    if (e.Position.Y < -10)
                {
                    e.Position.Y = Constants.BaseFloorY + rng.Next(5, 50);
                    e.Position.X = rng.Next(0, Constants.ScreenWidth);
                }
                embers[i] = e;
            }
        }

        public void SpawnBurst(Vector2 pos, Color color, int count = 10)
        {
            for (int i = 0; i < count; i++)
            {
                particles.Add(new Particle
                {
                    Position = pos,
                    Velocity = new Vector2(
                        (float)(rng.NextDouble() * 500 - 250), 
                        (float)(rng.NextDouble() * 500 - 300)
                    ),
                    MaxLife = 0.6f + (float)rng.NextDouble() * 0.4f,
                    Life = 0.6f + (float)rng.NextDouble() * 0.4f,
                    Size = 2f + (float)rng.NextDouble() * 4f,
                    Color = color
                });
            }
        }

        public void DrawAmbientEmbers(Color themeColor)
        {
            foreach (var e in embers)
            {
                float alpha = 0.15f + MathF.Sin(e.Phase) * 0.08f;
                Color c = themeColor;
                c.A = (byte)(alpha * 255);
                Raylib.DrawCircleV(e.Position, e.Size, c);
            }
        }

        public void DrawVignette(float darkness = 0.5f)
        {
            // Subtle fullscreen vignette using corner circles and translucent overlay
            int w = Constants.ScreenWidth;
            int h = Constants.ScreenHeight;

            // Base dim overlay
            Color baseDim = new Color((byte)0, (byte)0, (byte)0, (byte)(darkness * 80));
            Raylib.DrawRectangle(0, 0, w, h, baseDim);

            // Corner radial darkening
            Color corner = new Color((byte)0, (byte)0, (byte)0, (byte)(darkness * 140));
            int radius = Math.Max(w, h) / 2;
            Raylib.DrawCircle(0, 0, radius, corner);
            Raylib.DrawCircle(w, 0, radius, corner);
            Raylib.DrawCircle(0, h, radius, corner);
            Raylib.DrawCircle(w, h, radius, corner);
        }

        public void Draw(Color themeColor)
        {
            // Draw Running Motion Trail with Neon Blend
            for (int i = 0; i < trailPositions.Count - 1; i++)
            {
                float alpha = (float)i / trailPositions.Count;
                
                // Draw multi-layered bloom lines for trail
                Color glowOuter = themeColor;
                glowOuter.A = (byte)(alpha * 40);
                
                Color glowInner = themeColor;
                glowInner.A = (byte)(alpha * 120);

                Color core = Color.White;
                core.A = (byte)(alpha * 225);

                Vector2 p1 = trailPositions[i] + new Vector2(Constants.PlayerWidth / 2f, Constants.PlayerHeight / 2f);
                Vector2 p2 = trailPositions[i+1] + new Vector2(Constants.PlayerWidth / 2f, Constants.PlayerHeight / 2f);

                // Multi-pass line glow
                Raylib.DrawLineEx(p1, p2, 10f * alpha, glowOuter);
                Raylib.DrawLineEx(p1, p2, 5f * alpha, glowInner);
                Raylib.DrawLineEx(p1, p2, 2f * alpha, core);
            }

            // Draw Explosive Particles
            foreach (var p in particles)
            {
                float lifePercent = p.Life / p.MaxLife;
                Color c = p.Color;
                c.A = (byte)(lifePercent * 255);

                // Particle glow bloom
                Color outer = p.Color;
                outer.A = (byte)(lifePercent * 70);

                Raylib.DrawCircleV(p.Position, p.Size * 2.5f, outer);
                Raylib.DrawCircleV(p.Position, p.Size, Color.White);
            }
        }

        /// <summary>
        /// Renders a beautiful multi-pass neon bloom line.
        /// </summary>
        public static void DrawNeonLine(Vector2 start, Vector2 end, float baseThickness, Color themeColor)
        {
            Color outer = themeColor; outer.A = 35;
            Color mid = themeColor;   mid.A = 95;
            Color core = Color.White;

            Raylib.DrawLineEx(start, end, baseThickness * 4.5f, outer);
            Raylib.DrawLineEx(start, end, baseThickness * 2.0f, mid);
            Raylib.DrawLineEx(start, end, baseThickness * 0.8f, core);
        }

        /// <summary>
        /// Renders a beautiful multi-pass neon bloom circle.
        /// </summary>
        public static void DrawNeonCircle(Vector2 center, float radius, Color themeColor, bool fillCore = false)
        {
            Color outer = themeColor; outer.A = 25;
            Color mid = themeColor;   mid.A = 80;
            Color core = Color.White; core.A = 210;

            Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius + 3, outer);
            Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius + 1, mid);
            Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius, core);

            if (fillCore)
            {
                Color fill = themeColor;
                fill.A = 40;
                Raylib.DrawCircleV(center, radius, fill);
            }
        }

        public void ClearTrail() => trailPositions.Clear();
    }
}
