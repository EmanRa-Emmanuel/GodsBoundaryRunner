using Raylib_cs;

namespace GodsBoundaryRunner.Core
{
    public static class Constants
    {
        public const int ScreenWidth = 1280;
        public const int ScreenHeight = 720;
        public const float BaseFloorY = 580;
        public const float LevelLength = 150000f;
        
        public const float Gravity = 2200f;
        public const float JumpForce = -750f;
        public const float BaseRunSpeed = 450f;
        public const float MaxRunSpeed = 700f;
        public const float Acceleration = 300f;
        public const float Decceleration = 500f;
        public const float StumbleSpeed = 150f;
        public const float StumbleDuration = 1.5f;
        
        public const float SlideDuration = 0.6f;
        public const float PlayerWidth = 40f;
        public const float PlayerHeight = 80f;
        public const float PlayerSlideHeight = 40f;

        public static readonly Color SkyTopColor = new Color(11, 12, 16, 255);
        public static readonly Color SkyBottomColor = new Color(31, 18, 22, 255);
        public static readonly Color NightSkyBottomColor = new Color(15, 15, 35, 255);
    }
}
