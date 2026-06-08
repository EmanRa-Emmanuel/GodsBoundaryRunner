using System;
using System.Numerics;
using Raylib_cs;
using System.IO;
using GodsBoundaryRunner.Core;

namespace GodsBoundaryRunner.Entities
{
    public class PlayerController
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Width = Constants.PlayerWidth;
        public float Height = Constants.PlayerHeight;
        
        public bool IsGrounded = true;
        public bool IsSliding = false;
        public float SlideTimer = 0f;
        public float StumbleTimer = 0f;
        
        // Procedural Scale Squash & Stretch
        public float VisualScaleX = 1f;
        public float VisualScaleY = 1f;

        public bool IsFacingRight = true;
        public bool IsCrouching = false;

        public float RunPhase = 0f;
        public float StumbleSpinAngle = 0f;
        public float LandingRollTimer = 0f;
        private Vector2[] cloakSegments = new Vector2[5];
        private bool cloakInitialized = false;
        // Foot smoothing for planted foot stability (index 0 = left, 1 = right)
        private Vector2[] footPositions = new Vector2[2];
        private bool[] footInitialized = new bool[2] { false, false };
        // Optional sprite-sheet based running animation (frames horizontal)
        private Texture2D runSpriteSheet;
        private bool useSpriteRun = false;
        private int runSpriteFrames = 8; // override by naming convention later
        private float spriteAnimTime = 0f;
        private float spriteBaseFps = 12f;
        private int spriteFrameW = 0;
        private int spriteFrameH = 0;
        // Simple body inertia to simulate mass (affects root/hip offset)
        private Vector2 bodyOffset = Vector2.Zero;
        private float bodyMass = 2.0f; // larger = heavier / more sluggish

        public PlayerController(Vector2 startPos)
        {
            Position = startPos;
            Velocity = new Vector2(0f, 0f); // Wait for input!

            // Try to load an optional run sprite sheet from the game's assets folder.
            try
            {
                var baseDir = System.AppContext.BaseDirectory;
                var assetPath = Path.Combine(baseDir, "assets", "run_spritesheet.png");
                if (File.Exists(assetPath))
                {
                        // Load via Image to reliably obtain dimensions across Raylib_cs bindings
                        var img = Raylib.LoadImage(assetPath);
                        if (img.Width > 0 && img.Height > 0)
                        {
                            // Default: assume frames are horizontally laid out. Try to parse a simple naming override file.
                            useSpriteRun = true;
                            spriteFrameH = img.Height;
                            // If width is not divisible by runSpriteFrames, try to find frames from a metadata file
                            if (img.Width % runSpriteFrames == 0) spriteFrameW = img.Width / runSpriteFrames;
                            else
                            {
                                var meta = Path.Combine(baseDir, "assets", "run_spritesheet.meta");
                                if (File.Exists(meta))
                                {
                                    var txt = File.ReadAllText(meta).Trim();
                                    if (int.TryParse(txt, out var f) && f > 0 && img.Width % f == 0)
                                    {
                                        runSpriteFrames = f;
                                        spriteFrameW = img.Width / runSpriteFrames;
                                    }
                                    else
                                    {
                                        spriteFrameW = img.Width / runSpriteFrames;
                                    }
                                }
                                else
                                {
                                    spriteFrameW = img.Width / runSpriteFrames;
                                }
                            }

                            // Create GPU texture from image for rendering
                            runSpriteSheet = Raylib.LoadTextureFromImage(img);
                            Raylib.UnloadImage(img);
                        }
                }
            }
            catch { useSpriteRun = false; }
        }

        public void Update(float dt)
        {
            // Decaying Landing Roll
            if (LandingRollTimer > 0f)
            {
                LandingRollTimer -= dt;
            }

            // 1. HORIZONTAL INPUT & FACING DIRECTIONS
            float targetSpeed = 0f;
            bool pressRight = GodsBoundaryRunner.Core.InputManager.IsDown("MoveRight");
            bool pressLeft = GodsBoundaryRunner.Core.InputManager.IsDown("MoveLeft");
            bool pressDownInput = GodsBoundaryRunner.Core.InputManager.IsDown("Down");
            bool pressJumpPressed = GodsBoundaryRunner.Core.InputManager.IsPressed("Jump");

            if (StumbleTimer > 0)
            {
                StumbleTimer -= dt;
                StumbleSpinAngle += 720f * dt;
                // Move backward from facing direction when stumbled
                targetSpeed = IsFacingRight ? -Constants.StumbleSpeed : Constants.StumbleSpeed;
            }
            else if (IsSliding)
            {
                // Maintain high momentum slide in facing direction (respect speed multiplier)
                float slideSpeed = Constants.MaxRunSpeed * 1.4f * GodsBoundaryRunner.Core.SettingsManager.Config.MaxRunSpeedMultiplier;
                targetSpeed = IsFacingRight ? slideSpeed : -slideSpeed;
            }
            else
            {
                StumbleSpinAngle = 0f;

                if (pressRight && !pressLeft)
                {
                    targetSpeed = Constants.MaxRunSpeed * GodsBoundaryRunner.Core.SettingsManager.Config.MaxRunSpeedMultiplier;
                    IsFacingRight = true;
                }
                else if (pressLeft && !pressRight)
                {
                    targetSpeed = -Constants.MaxRunSpeed * GodsBoundaryRunner.Core.SettingsManager.Config.MaxRunSpeedMultiplier;
                    IsFacingRight = false;
                }
            }

            // Smooth Horizontal Interpolation
            if (MathF.Abs(targetSpeed) > 0f)
            {
                Velocity.X = Raymath.Lerp(Velocity.X, targetSpeed, 10f * dt);
            }
            else
            {
                // Slide-to-stop high-friction stop
                Velocity.X = Raymath.Lerp(Velocity.X, 0f, 15f * dt);
            }

            // Increment run cycle phase based on normalized speed (frame-rate stable)
            float speed = MathF.Abs(Velocity.X);
            float maxSpeed = Constants.MaxRunSpeed * GodsBoundaryRunner.Core.SettingsManager.Config.MaxRunSpeedMultiplier;
            float speedNorm = Math.Clamp(speed / MathF.Max(1f, maxSpeed), 0f, 1f);

            if (IsGrounded && !IsSliding && StumbleTimer <= 0 && LandingRollTimer <= 0f)
            {
                if (speed > 20f)
                {
                    // Phase rate scales nonlinearly with speed for punchier stride at high speed
                    float baseFreq = 2f * MathF.PI; // one full cycle base
                    float freq = baseFreq * (0.8f + speedNorm * 1.6f);
                    // Global tuning multiplier to control perceived run animation speed
                    // Reduced by 50% as requested (was 0.55)
                    const float runPhaseSpeedMultiplier = 0.275f;
                    RunPhase += freq * speedNorm * runPhaseSpeedMultiplier * dt;
                    // advance sprite animation time when using sprite sheet
                    if (useSpriteRun)
                    {
                        // Scale sprite playback speed with normalized speed so idle runs slower
                        float playSpeed = 0.5f + speedNorm * 1.5f;
                        spriteAnimTime += dt * spriteBaseFps * playSpeed;
                    }
                }
                else
                {
                    // Smoothly decay run phase when coming to a stop to blend into idle
                    RunPhase = Raymath.Lerp(RunPhase, 0f, 8f * dt);
                }

                // Keep phase bounded to avoid large accumulation
                if (RunPhase > MathF.PI * 2f || RunPhase < -MathF.PI * 2f) RunPhase %= (MathF.PI * 2f);
            }

            // 2. CROUCH logic (stationary crouching when Down is pressed while speed is low)
            if (IsGrounded && !IsSliding && StumbleTimer <= 0 && pressDownInput && MathF.Abs(Velocity.X) < 50f)
            {
                if (!IsCrouching)
                {
                    IsCrouching = true;
                    Height = Constants.PlayerSlideHeight;
                    Position.Y += (Constants.PlayerHeight - Constants.PlayerSlideHeight);
                }
            }
            else if (IsCrouching)
            {
                IsCrouching = false;
                Height = Constants.PlayerHeight;
                Position.Y -= (Constants.PlayerHeight - Constants.PlayerSlideHeight);
            }


            // Vertical movement: Jump impulse + gravity (no sustained flight)
            const float gravity = 1600f;
            const float maxFallSpeed = 900f;
            const float jumpImpulse = 520f;

            if (pressJumpPressed && IsGrounded && !IsSliding && StumbleTimer <= 0)
            {
                Velocity.Y = -jumpImpulse;
                IsGrounded = false;
            }

            // Apply gravity
            Velocity.Y += gravity * SettingsManager.Config.GravityMultiplier * dt;
            if (Velocity.Y > maxFallSpeed) Velocity.Y = maxFallSpeed;

            // Apply displacement
            Position.Y += Velocity.Y * dt;
            Position.X += Velocity.X * dt;

            // Restrict vertical flight within screen boundaries
            if (Position.Y < 40f)
            {
                Position.Y = 40f;
                if (Velocity.Y < 0f) Velocity.Y = 0f;
            }

            // Preserve previous grounded state for landing detection
            bool wasGrounded = IsGrounded;

            // Start boundary check: prevent running off the start of the level
            if (Position.X < 50f)
            {
                Position.X = 50f;
                if (Velocity.X < 0f) Velocity.X = 0f;
            }

            // Floor boundary collision limits and landing transition
            float floorLimit = Constants.BaseFloorY - Height;
            if (Position.Y >= floorLimit)
            {
                Position.Y = floorLimit;
                Velocity.Y = 0f;
                if (!wasGrounded)
                {
                    IsGrounded = true;
                    VisualScaleX = 1.25f; VisualScaleY = 0.78f; // Landing squash
                    LandingRollTimer = 0.4f; // Trigger parkour forward roll!
                }
                else
                {
                    IsGrounded = true;
                }
            }
            else
            {
                IsGrounded = false;
            }

            // Smooth visual scale restoration Lerp
            VisualScaleX = Raymath.Lerp(VisualScaleX, 1.0f, 12f * dt);
            VisualScaleY = Raymath.Lerp(VisualScaleY, 1.0f, 12f * dt);

            // Body inertia: lag the visual root based on velocity to give a sense of mass
            // Desired offset pulls opposite player velocity for a trailing feel
            Vector2 desiredBodyOffset = new Vector2(-Velocity.X * 0.02f, MathF.Min(6f, -Velocity.Y * 0.01f));
            // Inertia responsiveness scales inversely with mass
            float inertiaResponse = Math.Clamp(6f / MathF.Max(0.1f, bodyMass), 0.5f, 12f);
            bodyOffset = Vector2.Lerp(bodyOffset, desiredBodyOffset, inertiaResponse * dt);

            // Update Trailing Physics-Based Cloak
            UpdateCloak(dt);
        }

        private void UpdateCloak(float dt)
        {
            float facing = IsFacingRight ? 1f : -1f;
            // Lock anchor of cloak to the neck joint
            Vector2 neckPos = Position + new Vector2(Width * 0.4f, Height * 0.18f);
            if (IsSliding)
            {
                neckPos = Position + new Vector2(Width * (IsFacingRight ? 0.3f : 0.7f), Height * 0.4f);
            }

            if (!cloakInitialized)
            {
                for (int i = 0; i < cloakSegments.Length; i++)
                {
                    cloakSegments[i] = neckPos;
                }
                cloakInitialized = true;
            }

            cloakSegments[0] = neckPos;

            for (int i = 1; i < cloakSegments.Length; i++)
            {
                // Push segments backwards based on velocity (wind drag) and slightly upwards/downwards based on gravity and jump
                float windX = -Velocity.X * 0.05f * i;
                float verticalDrag = -Velocity.Y * 0.03f * i;

                // Adjust default drape offset based on facing direction
                Vector2 target = cloakSegments[i - 1] + new Vector2(windX - 6f * facing, 4f + verticalDrag);
                
                // Segment flex spring interpolation
                cloakSegments[i] = Vector2.Lerp(cloakSegments[i], target, 16f * dt);
            }
        }

        public Rectangle GetBounds()
        {
            return new Rectangle(Position.X, Position.Y, Width, Height);
        }

        public void Stumble()
        {
            if (StumbleTimer <= 0)
            {
                StumbleTimer = Constants.StumbleDuration;
                Velocity.X = Constants.StumbleSpeed / 2f;
                VisualScaleX = 0.8f; VisualScaleY = 0.8f;
            }
        }

        /// <summary>
        /// Draws a smooth, procedurally animated, jointed vector silhouette runner.
        /// Contrast highlights are added using the theme color.
        /// </summary>
        public void Draw(Color themeColor)
        {
            // Compute dimensions with squash & stretch
            float drawWidth = Width * VisualScaleX;
            float drawHeight = Height * VisualScaleY;
            
            // Core pivot coordinates
            Vector2 root = new Vector2(Position.X + Width / 2f, Position.Y + Height);
            // Apply body inertia offset to the visual root to simulate mass
            root += bodyOffset;
            
            // Adjust root Y offset for slide state
            if (IsSliding)
            {
                root.Y = Constants.BaseFloorY;
            }

            // 1. Draw physics-based dynamic matte-black cloak first (behind the runner body!)
            DrawDynamicCloak(themeColor);

            // If a sprite-sheet run animation is available, prefer it for running visuals
            if (useSpriteRun && IsGrounded && !IsSliding && StumbleTimer <= 0 && MathF.Abs(Velocity.X) > 20f)
            {
                // Determine current frame
                int frame = 0;
                if (runSpriteFrames > 0) frame = ((int)spriteAnimTime) % runSpriteFrames;

                // Source rectangle (flip Y because render coordinates)
                Rectangle src = new Rectangle(frame * spriteFrameW, 0, spriteFrameW * (IsFacingRight ? 1 : -1), -spriteFrameH);

                // Scale sprite to match player's height
                float scale = (Height * VisualScaleY) / (float)spriteFrameH;
                float destW = spriteFrameW * scale;
                float destH = spriteFrameH * scale;
                Rectangle dest = new Rectangle(root.X - destW * 0.5f, root.Y - destH, destW, destH);

                // Draw the frame
                Raylib.DrawTexturePro(runSpriteSheet, src, dest, new Vector2(0, 0), 0f, Color.White);
                return; // sprite fully represents the runner for this frame
            }

            // 2. Draw active jointed character pose layer
            if (StumbleTimer > 0)
            {
                DrawStumblingRagdoll(root, drawWidth, drawHeight, themeColor);
            }
            else if (LandingRollTimer > 0f)
            {
                DrawLandingRollPose(root, drawWidth, drawHeight, themeColor);
            }
            else if (IsSliding)
            {
                DrawSlidingPose(root, drawWidth, drawHeight, themeColor);
            }
            else if (!IsGrounded)
            {
                DrawFlyingPose(root, drawWidth, drawHeight, themeColor);
            }
            else if (IsCrouching)
            {
                DrawCrouchPose(root, drawWidth, drawHeight, themeColor);
            }
            else if (MathF.Abs(Velocity.X) < 15f && IsGrounded)
            {
                DrawIdlePose(root, drawWidth, drawHeight, themeColor);
            }
            else
            {
                DrawRunningPose(root, drawWidth, drawHeight, themeColor);
            }
        }

        private void DrawRunningPose(Vector2 root, float w, float h, Color themeColor)
        {
            float headRad = 9f;
            float facing = IsFacingRight ? 1f : -1f;

            // Normalize speed to scale bobbing and lean
            float speedNorm = Math.Clamp(MathF.Abs(Velocity.X) / (Constants.MaxRunSpeed * GodsBoundaryRunner.Core.SettingsManager.Config.MaxRunSpeedMultiplier), 0.1f, 1.2f);
            
            // Double-frequency body bobbing (weight sink on stance, rise on push)
            float bob = MathF.Cos(RunPhase * 2f) * (2.8f * speedNorm);

            // Sleek aerodynamic lean that increases naturally at full sprint
            float baseLean = 3f;
            float speedLean = speedNorm * 11f;
            float lean = baseLean + speedLean;

            // Elevated hip height (0.52f instead of 0.44f) to prevent crab-like crouching
            Vector2 hip = root - new Vector2(4f * facing, h * 0.52f - bob);
            Vector2 neck = root - new Vector2((lean - 4f) * facing, h * 0.84f - bob);
            Vector2 head = root - new Vector2((lean + 2f) * facing, h * 0.96f - bob);

            // Draw Head
            Raylib.DrawCircleV(head, headRad, Color.Black);
            Raylib.DrawCircleV(head + new Vector2(4f * facing, -2f), 2.5f, themeColor); // Glowing neon eye

            // Torso / Spine line
            Raylib.DrawLineEx(hip, neck, 11f, Color.Black);

            // Opposing leg phase cycles
            float leftPhase = RunPhase;
            float rightPhase = RunPhase + MathF.PI;

            // Draw legs (layered: outer leg left, inner leg right)
            DrawLeg(hip, leftPhase, true, themeColor);
            DrawLeg(hip, rightPhase, false, themeColor);

            // Draw arms in opposition to legs for natural gait balance
            Vector2 shoulder = neck - new Vector2(1f * facing, 4f);
            DrawArm(shoulder, rightPhase, false, themeColor); // Inner arm (right)
            DrawArm(shoulder, leftPhase, true, themeColor);  // Outer arm (left)
        }

        private void DrawFlyingPose(Vector2 root, float w, float h, Color themeColor)
        {
            float headRad = 9f;
            float facing = IsFacingRight ? 1f : -1f;

            // Superhero dynamic banking / diving pitch angle based on Y flight velocity
            float pitch = (Velocity.Y / 350f) * 0.35f * facing;
            float cos = MathF.Cos(pitch);
            float sin = MathF.Sin(pitch);

            Vector2 center = root - new Vector2(0f, h * 0.45f);

            // Rotation helper around center of mass
            Vector2 Rotate(Vector2 offset)
            {
                return center + new Vector2(
                    offset.X * cos - offset.Y * sin,
                    offset.X * sin + offset.Y * cos
                );
            }

            // Horizontal spine offsets
            Vector2 hip = Rotate(new Vector2(-16f * facing, 0f));
            Vector2 neck = Rotate(new Vector2(16f * facing, 0f));
            Vector2 head = Rotate(new Vector2(26f * facing, 0f));

            // Head and glowing superhero eyes
            Raylib.DrawCircleV(head, headRad, Color.Black);
            Raylib.DrawCircleV(Rotate(new Vector2(30f * facing, -2f)), 2.5f, themeColor);

            // Horizontal Torso
            Raylib.DrawLineEx(hip, neck, 11f, Color.Black);

            // Reaching Arms: Fully extended forward like Superman!
            Vector2 lHand = Rotate(new Vector2(44f * facing, -2f));
            Raylib.DrawLineEx(neck, lHand, 6.2f, Color.Black);
            Raylib.DrawCircleV(lHand, 3.0f, Color.Black);

            Vector2 rHand = Rotate(new Vector2(41f * facing, 2f));
            Raylib.DrawLineEx(neck, rHand, 4.6f, Color.Black);
            Raylib.DrawCircleV(rHand, 2.2f, Color.Black);

            // Extended Trailing Legs: Pointing straight back behind the hip
            Vector2 lFoot = Rotate(new Vector2(-40f * facing, -2f));
            Raylib.DrawLineEx(hip, lFoot, 8.0f, Color.Black);
            Raylib.DrawCircleV(lFoot, 4.5f, Color.Black);

            Vector2 rFoot = Rotate(new Vector2(-38f * facing, 2f));
            Raylib.DrawLineEx(hip, rFoot, 6.5f, Color.Black);
            Raylib.DrawCircleV(rFoot, 3.6f, Color.Black);
        }

        private void DrawSlidingPose(Vector2 root, float w, float h, Color themeColor)
        {
            float headRad = 8.5f;
            float facing = IsFacingRight ? 1f : -1f;

            // Low, dynamic, leaning back parkour slide pose
            Vector2 hip = root - new Vector2(8f * facing, 12f);
            Vector2 neck = root - new Vector2(-16f * facing, 22f);
            Vector2 head = root - new Vector2(-22f * facing, 32f);

            // Draw Head
            Raylib.DrawCircleV(head, headRad, Color.Black);
            Raylib.DrawCircleV(head + new Vector2(3f * facing, -1f), 2.2f, themeColor);

            // Leaning Torso
            Raylib.DrawLineEx(hip, neck, 12f, Color.Black);

            // Leading Leg: Extended fully forward, skimming the floor
            Vector2 lKnee = hip + new Vector2(24f * facing, 3f);
            Vector2 lFoot = lKnee + new Vector2(22f * facing, 5f);
            Raylib.DrawLineEx(hip, lKnee, 8.5f, Color.Black);
            Raylib.DrawLineEx(lKnee, lFoot, 6.0f, Color.Black);
            Raylib.DrawCircleV(lFoot, 4.5f, Color.Black);

            // Trailing Leg: Bent tightly under the hip for slide balance
            Vector2 rKnee = hip + new Vector2(-10f * facing, 5f);
            Vector2 rFoot = rKnee + new Vector2(-12f * facing, 10f);
            Raylib.DrawLineEx(hip, rKnee, 6.8f, Color.Black);
            Raylib.DrawLineEx(rKnee, rFoot, 4.6f, Color.Black);
            Raylib.DrawCircleV(rFoot, 3.6f, Color.Black);

            // Planted arm on floor pushing forward for balance support, and high-aerodynamic leading arm
            Vector2 shoulder = neck - new Vector2(2f * facing, 4f);

            // Planted Arm (Back support)
            Vector2 lElbow = shoulder + new Vector2(-12f * facing, 8f);
            Vector2 lHand = lElbow + new Vector2(-6f * facing, 10f);
            Raylib.DrawLineEx(shoulder, lElbow, 6.2f, Color.Black);
            Raylib.DrawLineEx(lElbow, lHand, 4.4f, Color.Black);

            // Front Arm (Style / Reach)
            Vector2 rElbow = shoulder + new Vector2(14f * facing, -4f);
            Vector2 rHand = rElbow + new Vector2(12f * facing, 2f);
            Raylib.DrawLineEx(shoulder, rElbow, 4.6f, Color.Black);
            Raylib.DrawLineEx(rElbow, rHand, 3.0f, Color.Black);
        }

        private void DrawStumblingRagdoll(Vector2 root, float w, float h, Color themeColor)
        {
            // Spin entire body around center of mass
            float angleRad = StumbleSpinAngle * MathF.PI / 180f;
            Vector2 center = root - new Vector2(0, h * 0.5f);
            
            float cos = MathF.Cos(angleRad);
            float sin = MathF.Sin(angleRad);

            // Helper to rotate relative vectors
            Vector2 Rotate(Vector2 offset)
            {
                return center + new Vector2(
                    offset.X * cos - offset.Y * sin,
                    offset.X * sin + offset.Y * cos
                );
            }

            Vector2 hip = Rotate(new Vector2(0, h * 0.1f));
            Vector2 neck = Rotate(new Vector2(0, -h * 0.3f));
            Vector2 head = Rotate(new Vector2(0, -h * 0.45f));

            // Head
            Raylib.DrawCircleV(head, 9f, Color.Black);
            Raylib.DrawCircleV(head, 3f, themeColor);

            // Spine
            Raylib.DrawLineEx(hip, neck, 11f, Color.Black);

            // Flailing Legs
            Vector2 lKnee = Rotate(new Vector2(-20, h * 0.3f));
            Vector2 lFoot = Rotate(new Vector2(-10, h * 0.5f));
            Raylib.DrawLineEx(hip, lKnee, 8f, Color.Black);
            Raylib.DrawLineEx(lKnee, lFoot, 6f, Color.Black);

            Vector2 rKnee = Rotate(new Vector2(15, h * 0.25f));
            Vector2 rFoot = Rotate(new Vector2(30, h * 0.45f));
            Raylib.DrawLineEx(hip, rKnee, 8f, Color.Black);
            Raylib.DrawLineEx(rKnee, rFoot, 6f, Color.Black);

            // Flailing Arms
            Vector2 shoulder = neck;
            Vector2 lElbow = Rotate(new Vector2(-15, -h * 0.15f));
            Vector2 lHand = Rotate(new Vector2(-25, -h * 0.3f));
            Raylib.DrawLineEx(shoulder, lElbow, 7f, Color.Black);
            Raylib.DrawLineEx(lElbow, lHand, 5f, Color.Black);
        }

        private void DrawLandingRollPose(Vector2 root, float w, float h, Color themeColor)
        {
            // Compact parkour forward landing roll
            float progress = 1f - (LandingRollTimer / 0.4f);
            float rollAngle = progress * 360f * MathF.PI / 180f;
            float facing = IsFacingRight ? 1f : -1f;

            Vector2 pivot = root - new Vector2(0, h * 0.33f);

            Vector2 RotateOffset(Vector2 offset)
            {
                float cos = MathF.Cos(rollAngle);
                float sin = MathF.Sin(rollAngle);
                // Flip offset.X based on facing direction before rotating!
                float ox = offset.X * facing;
                return pivot + new Vector2(
                    ox * cos - offset.Y * sin,
                    ox * sin + offset.Y * cos
                );
            }

            // Tucked ball nodes
            Vector2 head = RotateOffset(new Vector2(10f, -8f));
            Vector2 neck = RotateOffset(new Vector2(5f, -2f));
            Vector2 hip = RotateOffset(new Vector2(-5f, 5f));

            // Torso Spine
            Raylib.DrawLineEx(hip, neck, 13f, Color.Black);
            
            // Head
            Raylib.DrawCircleV(head, 9f, Color.Black);
            Raylib.DrawCircleV(head + new Vector2(3f * facing, -1f), 2.5f, themeColor); // Eye

            // Left leg tucked deeply
            Vector2 lKnee = RotateOffset(new Vector2(-15f, -12f));
            Vector2 lFoot = RotateOffset(new Vector2(-8f, -20f));
            Raylib.DrawLineEx(hip, lKnee, 8.5f, Color.Black);
            Raylib.DrawLineEx(lKnee, lFoot, 6.0f, Color.Black);
            Raylib.DrawCircleV(lFoot, 4.5f, Color.Black);

            // Right leg tucked
            Vector2 rKnee = RotateOffset(new Vector2(0f, -18f));
            Vector2 rFoot = RotateOffset(new Vector2(8f, -18f));
            Raylib.DrawLineEx(hip, rKnee, 6.8f, Color.Black);
            Raylib.DrawLineEx(rKnee, rFoot, 4.6f, Color.Black);
            Raylib.DrawCircleV(rFoot, 3.6f, Color.Black);

            // Tucked Arms
            Vector2 shoulder = neck;
            
            Vector2 lElbow = RotateOffset(new Vector2(15f, 4f));
            Vector2 lHand = lElbow + new Vector2(0f, 4f);
            Raylib.DrawLineEx(shoulder, lElbow, 6.2f, Color.Black);
            Raylib.DrawLineEx(lElbow, lHand, 4.4f, Color.Black);

            Vector2 rElbow = RotateOffset(new Vector2(12f, -10f));
            Vector2 rHand = rElbow + new Vector2(0f, -4f);
            Raylib.DrawLineEx(shoulder, rElbow, 4.6f, Color.Black);
            Raylib.DrawLineEx(rElbow, rHand, 3.0f, Color.Black);
        }

        private void DrawIdlePose(Vector2 root, float w, float h, Color themeColor)
        {
            float headRad = 9f;
            float facing = IsFacingRight ? 1f : -1f;

            // Faint breathing cycle
            float breath = MathF.Sin((float)Raylib.GetTime() * 2.5f) * 1.0f;

            Vector2 hip = root - new Vector2(0f, h * 0.45f);
            Vector2 neck = root - new Vector2(-2f * facing, h * 0.82f - breath * 0.5f);
            Vector2 head = root - new Vector2(-3f * facing, h * 0.94f - breath * 0.8f);

            // Draw Head
            Raylib.DrawCircleV(head, headRad, Color.Black);
            Raylib.DrawCircleV(head + new Vector2(3f * facing, -1f), 2.5f, themeColor); // Eye

            // Torso
            Raylib.DrawLineEx(hip, neck, 11f, Color.Black);

            // Legs slightly apart in a solid standing stance
            Vector2 lKnee = hip + new Vector2(-6f * facing, 18f);
            Vector2 lFoot = lKnee + new Vector2(-10f * facing, 20f);
            Raylib.DrawLineEx(hip, lKnee, 8.5f, Color.Black);
            Raylib.DrawLineEx(lKnee, lFoot, 6.0f, Color.Black);
            Raylib.DrawCircleV(lFoot, 4.5f, Color.Black);

            Vector2 rKnee = hip + new Vector2(6f * facing, 18f);
            Vector2 rFoot = rKnee + new Vector2(8f * facing, 20f);
            Raylib.DrawLineEx(hip, rKnee, 6.8f, Color.Black);
            Raylib.DrawLineEx(rKnee, rFoot, 4.6f, Color.Black);
            Raylib.DrawCircleV(rFoot, 3.6f, Color.Black);

            // Arms relaxed at side
            Vector2 shoulder = neck - new Vector2(1f * facing, 4f);
            Vector2 lElbow = shoulder + new Vector2(-3f * facing, 15f);
            Vector2 lHand = lElbow + new Vector2(0f, 15f);
            Raylib.DrawLineEx(shoulder, lElbow, 6.2f, Color.Black);
            Raylib.DrawLineEx(lElbow, lHand, 4.4f, Color.Black);

            Vector2 rElbow = shoulder + new Vector2(3f * facing, 15f);
            Vector2 rHand = rElbow + new Vector2(0f, 15f);
            Raylib.DrawLineEx(shoulder, rElbow, 4.6f, Color.Black);
            Raylib.DrawLineEx(rElbow, rHand, 3.0f, Color.Black);
        }

        private void DrawCrouchPose(Vector2 root, float w, float h, Color themeColor)
        {
            float headRad = 9f;
            float facing = IsFacingRight ? 1f : -1f;

            // Lowered posture: hip is very low to floor
            Vector2 hip = root - new Vector2(5f * facing, h * 0.22f);
            Vector2 neck = root - new Vector2(10f * facing, h * 0.46f);
            Vector2 head = root - new Vector2(14f * facing, h * 0.58f);

            // Draw Head
            Raylib.DrawCircleV(head, headRad, Color.Black);
            Raylib.DrawCircleV(head + new Vector2(3f * facing, -1f), 2.5f, themeColor); // Eye

            // Torso
            Raylib.DrawLineEx(hip, neck, 11f, Color.Black);

            // Crouched tucked legs
            Vector2 lKnee = hip + new Vector2(-15f * facing, -5f);
            Vector2 lFoot = lKnee + new Vector2(10f * facing, 22f);
            Raylib.DrawLineEx(hip, lKnee, 8.5f, Color.Black);
            Raylib.DrawLineEx(lKnee, lFoot, 6.0f, Color.Black);
            Raylib.DrawCircleV(lFoot, 4.5f, Color.Black);

            Vector2 rKnee = hip + new Vector2(8f * facing, -3f);
            Vector2 rFoot = rKnee + new Vector2(-4f * facing, 20f);
            Raylib.DrawLineEx(hip, rKnee, 6.8f, Color.Black);
            Raylib.DrawLineEx(rKnee, rFoot, 4.6f, Color.Black);
            Raylib.DrawCircleV(rFoot, 3.6f, Color.Black);

            // Arms touching the floor for parkour three-point crouch balance!
            Vector2 shoulder = neck - new Vector2(1f * facing, 2f);
            Vector2 lElbow = shoulder + new Vector2(12f * facing, 10f);
            Vector2 lHand = lElbow + new Vector2(4f * facing, 12f);
            Raylib.DrawLineEx(shoulder, lElbow, 6.2f, Color.Black);
            Raylib.DrawLineEx(lElbow, lHand, 4.4f, Color.Black);

            Vector2 rElbow = shoulder + new Vector2(-10f * facing, 8f);
            Vector2 rHand = rElbow + new Vector2(-4f * facing, 14f);
            Raylib.DrawLineEx(shoulder, rElbow, 4.6f, Color.Black);
            Raylib.DrawLineEx(rElbow, rHand, 3.0f, Color.Black);
        }

        private void DrawDynamicCloak(Color themeColor)
        {
            if (!cloakInitialized) return;

            // Connected matte-black segments tapering behind, with glowing active theme neon hems!
            for (int i = 0; i < cloakSegments.Length - 1; i++)
            {
                float w1 = 14f - i * 2.2f;
                float w2 = 14f - (i + 1) * 2.2f;

                // Base solid black drape
                Raylib.DrawLineEx(cloakSegments[i], cloakSegments[i+1], w1, Color.Black);
                
                // Razor-thin neon glowing edge trailing the outer contours
                Color neonCol = themeColor;
                neonCol.A = 120; // Soft neon trailing edge
                Raylib.DrawLineEx(cloakSegments[i] + new Vector2(0, w1 * 0.4f), cloakSegments[i+1] + new Vector2(0, w2 * 0.4f), 1.8f, neonCol);
            }
        }

        private void DrawLeg(Vector2 hip, float phase, bool leftLeg, Color themeColor)
        {
            float speedRatio = Math.Clamp(MathF.Abs(Velocity.X) / Constants.MaxRunSpeed, 0f, 1f);
            
            // Adjusted leg lengths (shorter) to fit perfectly with the raised hip height and prevent extreme knee bending
            float thighLen = 17f + speedRatio * 3f;
            float shinLen = 15f + speedRatio * 2f;
            float facing = IsFacingRight ? 1f : -1f;

            // Phase shifted for natural plant timing
            // Keep phase within 0 to 2*PI
            float p = (phase + 0.2f) % (2f * MathF.PI);
            if (p < 0f) p += 2f * MathF.PI;

            float baseStride = 20f;
            float stride = baseStride + speedRatio * 16f;
            float lift = 9f + speedRatio * 5f;

            float footOffsetX = 0f;
            float footOffsetY = 0f;

            float floorY = Constants.BaseFloorY;
            Vector2 worldFootTarget;

            // Split into Stance and Swing phases for realistic running kinematics!
            if (p < MathF.PI)
            {
                // STANCE PHASE: Foot plants and drives backwards along the ground
                float stanceT = p / MathF.PI;
                footOffsetX = (stride * 0.45f - stride * 0.9f * stanceT) * facing;
                worldFootTarget = new Vector2(hip.X + footOffsetX, floorY);
            }
            else
            {
                // SWING PHASE: Leg lifts high, tucks knee, sweeps forward
                float swingT = (p - MathF.PI) / MathF.PI;
                footOffsetX = (-stride * 0.45f + stride * 0.9f * swingT) * facing;
                
                // Elliptic high lift during middle swing phase
                footOffsetY = -MathF.Sin(swingT * MathF.PI) * lift;
                worldFootTarget = new Vector2(hip.X + footOffsetX, floorY + footOffsetY);
                
                // Deep knee tuck during mid-swing: pull thigh length slightly shorter visually to represent leg folding!
                thighLen -= MathF.Sin(swingT * MathF.PI) * 3f;
            }

            // Inverse kinematics for leg joints (Knee & Foot)
            Vector2 toFoot = worldFootTarget - hip;
            float dist = MathF.Sqrt(toFoot.X * toFoot.X + toFoot.Y * toFoot.Y);
            float maxReach = thighLen + shinLen - 0.001f;
            float minReach = MathF.Abs(thighLen - shinLen) + 0.001f;
            float d = Math.Clamp(dist, minReach, maxReach);

            float hipToFootAngle = MathF.Atan2(toFoot.X, toFoot.Y);

            float cosK = Math.Clamp((thighLen * thighLen + shinLen * shinLen - d * d) / (2f * thighLen * shinLen), -1f, 1f);
            float kneeInner = MathF.Acos(cosK);
            float ext = MathF.PI - kneeInner;
            
            // Adjust knee bend dampening
            ext *= 0.88f;

            float cosA = Math.Clamp((thighLen * thighLen + d * d - shinLen * shinLen) / (2f * thighLen * d), -1f, 1f);
            float angleAtHip = MathF.Acos(cosA);

            // Choose knee bend direction so knees bend forward (human-like) relative to facing direction
            float bendDir = MathF.Sign(facing);

            float thighAngle = hipToFootAngle + bendDir * angleAtHip;
            float shinAngle = thighAngle - bendDir * ext;

            Vector2 knee = hip + new Vector2(MathF.Sin(thighAngle) * thighLen, MathF.Cos(thighAngle) * thighLen);
            Vector2 foot = knee + new Vector2(MathF.Sin(shinAngle) * shinLen, MathF.Cos(shinAngle) * shinLen);

            // Ensure foot locks perfectly to floor during stance phase
            if (p < MathF.PI)
            {
                foot.Y = floorY;
            }

            // Depth cue widths (outer leg left is thicker for beautiful silhouette spacing)
            float thighWidth = leftLeg ? 8.5f : 6.8f;
            float shinWidth = leftLeg ? 6.0f : 4.6f;
            float shoeRadius = leftLeg ? 4.5f : 3.6f;

            Raylib.DrawLineEx(hip, knee, thighWidth, Color.Black);
            Raylib.DrawLineEx(knee, foot, shinWidth, Color.Black);
            Raylib.DrawCircleV(foot, shoeRadius, Color.Black);
        }

        private void DrawArm(Vector2 shoulder, float phase, bool leftArm, Color themeColor)
        {
            float bicepLen = 14f;
            float forearmLen = 14f;
            float facing = IsFacingRight ? 1f : -1f;

            // Pump angle is based on running phase, offset by opposition
            float swing = MathF.Sin(phase);
            
            // Upper arm swing
            float bicepAngle = (-0.6f * swing + 0.15f) * facing;
            
            // Elbow stays bent naturally for a dynamic sprint cycle
            float elbowAngle = bicepAngle + (1.4f + swing * 0.35f) * facing;

            Vector2 elbow = shoulder + new Vector2(MathF.Sin(bicepAngle) * bicepLen, MathF.Cos(bicepAngle) * bicepLen);
            Vector2 hand = elbow + new Vector2(MathF.Sin(elbowAngle) * forearmLen, MathF.Cos(elbowAngle) * forearmLen);

            // Depth cue arm widths
            float armWidth1 = leftArm ? 6.2f : 4.6f;
            float armWidth2 = leftArm ? 4.4f : 3.0f;
            float handRadius = leftArm ? 3.0f : 2.2f;

            Raylib.DrawLineEx(shoulder, elbow, armWidth1, Color.Black);
            Raylib.DrawLineEx(elbow, hand, armWidth2, Color.Black);
            Raylib.DrawCircleV(hand, handRadius, Color.Black);
        }
    }
}
