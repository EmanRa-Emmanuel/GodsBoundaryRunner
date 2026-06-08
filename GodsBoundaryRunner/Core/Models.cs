using System.Numerics;
using Raylib_cs;

namespace GodsBoundaryRunner.Core
{
    public enum ObstacleType
    {
        Standard,
        Tunnel,
        Pendulum,
        SolarRay,
        SinkingSpire,
        FlyingOrb,
        EyeOfRa,
        Gate,
        Checkpoint
    }

    public struct Obstacle
    {
        public Rectangle Rect;
        public ObstacleType Type;
        public bool Active;
        
        // Pendulum specifics
        public Vector2 Hinge;
        public float Length;
        public float SwingAngle;
        public float SwingSpeed;
        public float BasePhase;

        // Solar Ray specifics
        public float WarningTimer;
        public float ActiveTimer;
        public bool IsFired;

        // Sinking Spire specifics
        public float TargetY;
        public float CurrentY;
        public float InitialY;
        public bool IsTriggered;
        
        // Flying Orb specifics
        public float FlySpeed;
        public float SineAmp;
        public float SineFreq;
    }

    public struct Ankh
    {
        public Vector2 Position;
        public bool Collected;
        public float FloatOffset; // Floating animation phase
        public float RotationAngle;
    }

    public struct LevelData
    {
        public string Name;
        public Color ThemeColor;
        public LevelID ID;
        public string Description;
    }
}
