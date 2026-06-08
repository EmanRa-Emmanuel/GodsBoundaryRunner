using System;
using System.Numerics;
using Raylib_cs;
using GodsBoundaryRunner.Core;
using GodsBoundaryRunner.Systems;

namespace GodsBoundaryRunner.UI
{
    public class UIOverlay
    {
        private float transitionBannerProgress = 0f;
        private string transitionBannerTitle = "";
        private string transitionBannerSub = "";
        // Remapping UI state
        private bool isRemapping = false;
        private string remapAction = "";
        private readonly System.Collections.Generic.Dictionary<string, Rectangle> actionRects = new System.Collections.Generic.Dictionary<string, Rectangle>();

        public void StartLevelBanner(string title, string desc)
        {
            transitionBannerTitle = title;
            transitionBannerSub = desc;
            transitionBannerProgress = 3.5f; // Holds for 3.5 seconds
        }

        public void Update(float dt)
        {
            if (transitionBannerProgress > 0)
            {
                transitionBannerProgress -= dt;
            }
        }

        public bool IsRemapping => isRemapping;
        public string RemapAction => remapAction;

        public void StartRemap(string action)
        {
            isRemapping = true;
            remapAction = action;
        }

        public void CancelRemap()
        {
            isRemapping = false;
            remapAction = "";
        }

        public string? GetActionAtPoint(Vector2 p)
        {
            foreach (var kv in actionRects)
            {
                if (Raylib.CheckCollisionPointRec(p, kv.Value)) return kv.Key;
            }
            return null;
        }

        public void DrawHUD(LevelData level, int ankhs, float progress, float maxRunSpeed, float currentSpeed)
        {
            // Glassmorphism HUD Bar Background
            Raylib.DrawRectangleGradientV(0, 0, Constants.ScreenWidth, 110, new Color(11, 12, 16, 230), new Color(11, 12, 16, 0));
            Raylib.DrawLineEx(new Vector2(0, 110), new Vector2(Constants.ScreenWidth, 110), 1f, new Color(255, 255, 255, 20));

            // Episode Label & Title
            string epText = $"EPISODE 0{(int)level.ID + 1}";
            Raylib.DrawText(epText, 25, 20, 12, level.ThemeColor);
            Raylib.DrawText(level.Name.ToUpper(), 25, 36, 26, Color.White);

            // Ankh counter with premium layout
            int panelX = Constants.ScreenWidth - 280;
            Raylib.DrawRectangleRounded(new Rectangle(panelX, 22, 250, 48), 0.25f, 4, new Color(20, 20, 30, 180));
            Raylib.DrawRectangleRoundedLines(new Rectangle(panelX, 22, 250, 48), 0.25f, 4, 1.5f, level.ThemeColor);
            
            // Neon glowing Ankh primitive
            Raylib.DrawCircle(panelX + 25, 46, 8, Color.Gold);
            Raylib.DrawCircleLines(panelX + 25, 46, 12, level.ThemeColor);
            Raylib.DrawText("ANKH WALLET", panelX + 50, 28, 10, Color.LightGray);
            Raylib.DrawText($"{ankhs:D4}", panelX + 50, 40, 22, Color.Gold);

            // Progress Bar / Distance Gauge
            float progressPercent = Math.Clamp(progress / Constants.LevelLength, 0f, 1f);
            int barWidth = 400;
            int barX = Constants.ScreenWidth / 2 - barWidth / 2;
            int barY = 40;

            // Draw track label
            Raylib.DrawText("TEMPLE BOUNDARY METRICS", barX, 22, 10, Color.Gray);
            Raylib.DrawText($"{(int)progress}m / {(int)Constants.LevelLength}m", barX + barWidth - 100, 22, 10, Color.LightGray);

            // Draw Track gauge
            Raylib.DrawRectangleRounded(new Rectangle(barX, barY, barWidth, 8), 0.5f, 4, new Color(40, 40, 50, 150));
            Raylib.DrawRectangleRounded(new Rectangle(barX, barY, barWidth * progressPercent, 8), 0.5f, 4, level.ThemeColor);
            // Dynamic node at play progress
            Raylib.DrawCircle(barX + (int)(barWidth * progressPercent), barY + 4, 6, Color.White);

            // Level Transition Banner
            if (transitionBannerProgress > 0)
            {
                DrawTransitionBanner(level.ThemeColor);
            }
        }

        public void DrawLoading(float progress)
        {
            // Simple centered loading bar with spinner and cancel hint
            int w = 600;
            int h = 28;
            int x = Constants.ScreenWidth / 2 - w / 2;
            int y = Constants.ScreenHeight / 2 - 40;

            Raylib.DrawRectangleRounded(new Rectangle(x, y, w, h), 0.12f, 4, new Color(18, 18, 20, 220));
            Raylib.DrawRectangleRoundedLines(new Rectangle(x, y, w, h), 0.12f, 4, 1.5f, Color.LightGray);
            Raylib.DrawRectangle(x + 4, y + 4, (int)((w - 8) * progress), h - 8, new Color(40, 200, 120, 220));

            string label = $"LOADING... {(int)(progress * 100)}%";
            Raylib.DrawText(label, x + w / 2 - Raylib.MeasureText(label, 18) / 2, y - 28, 18, Color.White);

            // Spinner (6 dots rotating)
            float t = (float)Raylib.GetTime();
            int cx = Constants.ScreenWidth / 2;
            int cy = y + h + 30;
            int dotCount = 6;
            float radius = 18f;
            for (int i = 0; i < dotCount; i++)
            {
                float angle = t * 6f + i * (MathF.PI * 2f / dotCount);
                float sx = cx + MathF.Cos(angle) * radius;
                float sy = cy + MathF.Sin(angle) * radius;
                float alpha = 0.3f + 0.7f * ((i + (t * 6f % dotCount)) % dotCount) / (dotCount - 1);
                Color c = Color.White;
                c.A = (byte)(Math.Clamp(alpha, 0f, 1f) * 255);
                Raylib.DrawCircle((int)sx, (int)sy, 5, c);
            }

            // Cancel hint
            string hint = "Press ESC to cancel loading";
            Raylib.DrawText(hint, Constants.ScreenWidth / 2 - Raylib.MeasureText(hint, 14) / 2, cy + 26, 14, Color.LightGray);
        }

        private void DrawTransitionBanner(Color themeColor)
        {
            float alpha = 1.0f;
            if (transitionBannerProgress < 0.5f)
            {
                alpha = transitionBannerProgress / 0.5f; // Fade out
            }
            else if (transitionBannerProgress > 3.0f)
            {
                alpha = (3.5f - transitionBannerProgress) / 0.5f; // Fade in
            }

            int bgY = Constants.ScreenHeight / 2 - 100;
            Color panelBg = new Color(10, 10, 15, (int)(alpha * 240));
            Color neonColor = themeColor;
            neonColor.A = (byte)(alpha * 255);

            Raylib.DrawRectangle(0, bgY, Constants.ScreenWidth, 200, panelBg);
            
            // Draw dual glowing horizontal lines
            FXSystem.DrawNeonLine(new Vector2(0, bgY), new Vector2(Constants.ScreenWidth, bgY), 2f, themeColor);
            FXSystem.DrawNeonLine(new Vector2(0, bgY + 200), new Vector2(Constants.ScreenWidth, bgY + 200), 2f, themeColor);

            // Slide text animation offset
            float slideOffset = (transitionBannerProgress > 3.0f) ? (transitionBannerProgress - 3.0f) * 150f : 0f;

            Raylib.DrawText(transitionBannerTitle.ToUpper(), (int)(Constants.ScreenWidth / 2 - Raylib.MeasureText(transitionBannerTitle, 36) / 2 - slideOffset), bgY + 45, 36, Color.White);
            Raylib.DrawText(transitionBannerSub, (int)(Constants.ScreenWidth / 2 - Raylib.MeasureText(transitionBannerSub, 16) / 2 + slideOffset), bgY + 110, 16, themeColor);
        }

        public void DrawMenu(Color themeColor)
        {
            // Fully black cinematic overlay
            Raylib.DrawRectangle(0, 0, Constants.ScreenWidth, Constants.ScreenHeight, new Color(11, 12, 16, 245));

            // Ambient background layout grid lines
            for (int y = 50; y < Constants.ScreenHeight; y += 100)
            {
                Color gridCol = themeColor;
                gridCol.A = 12;
                Raylib.DrawLine(0, y, Constants.ScreenWidth, y, gridCol);
            }

            // Cinematic Menu Content
            int titleY = 220;
            Raylib.DrawText("G . O . D . S", Constants.ScreenWidth / 2 - Raylib.MeasureText("G . O . D . S", 48) / 2, titleY, 48, Color.White);
            
            // Massive glowing neon title
            string subText = "BOUNDARY RUNNER";
            int subTextWidth = Raylib.MeasureText(subText, 36);
            int subX = Constants.ScreenWidth / 2 - subTextWidth / 2;
            
            // Neon bloom under title
            FXSystem.DrawNeonLine(new Vector2(subX - 40, titleY + 75), new Vector2(subX + subTextWidth + 40, titleY + 75), 3f, themeColor);
            Raylib.DrawText(subText, subX, titleY + 55, 36, themeColor);

            Raylib.DrawText("SEASON 1: RISE OF THE NTR", Constants.ScreenWidth / 2 - Raylib.MeasureText("SEASON 1: RISE OF THE NTR", 18) / 2, titleY + 115, 18, Color.Gray);

            // Start Prompt button panel
            int btnWidth = 320;
            int btnHeight = 50;
            int btnX = Constants.ScreenWidth / 2 - btnWidth / 2;
            int btnY = 460;

            // Pulse opacity of key prompt using time
            float pulse = 0.5f + MathF.Sin((float)Raylib.GetTime() * 4.5f) * 0.5f;
            Color promptColor = Color.White;
            promptColor.A = (byte)(100 + pulse * 155);

            Raylib.DrawRectangleRounded(new Rectangle(btnX, btnY, btnWidth, btnHeight), 0.25f, 4, new Color(20, 20, 30, 200));
            Raylib.DrawRectangleRoundedLines(new Rectangle(btnX, btnY, btnWidth, btnHeight), 0.25f, 4, 1.5f, themeColor);
            
            Raylib.DrawText("PRESS SPACEBAR TO INITIATE RUN", btnX + 30, btnY + 18, 14, promptColor);

            // High Fidelity Controls Badge
            int ctrlX = Constants.ScreenWidth / 2 - 250;
            int ctrlY = Constants.ScreenHeight - 110;
            Raylib.DrawRectangleRounded(new Rectangle(ctrlX, ctrlY, 500, 70), 0.15f, 4, new Color(15, 15, 20, 180));
            Raylib.DrawRectangleRoundedLines(new Rectangle(ctrlX, ctrlY, 500, 70), 0.15f, 4, 1f, new Color(255, 255, 255, 20));

            Raylib.DrawText("PILOT CONTROLS MATRIX", ctrlX + 175, ctrlY + 12, 11, Color.LightGray);
            Raylib.DrawText("[SPACE] or [UP] : VAULT JUMP   |   [DOWN] or [CTRL] : SPEED SLIDE", ctrlX + 45, ctrlY + 36, 13, Color.Gray);
        }

        public void DrawPauseMenu()
        {
            // Semi-transparent overlay
            Raylib.DrawRectangle(0, 0, Constants.ScreenWidth, Constants.ScreenHeight, new Color(8, 8, 10, 220));

            string title = "PAUSED";
            Raylib.DrawText(title, Constants.ScreenWidth / 2 - Raylib.MeasureText(title, 48) / 2, 120, 48, Color.White);

            string gText = $"Gravity Multiplier: {GodsBoundaryRunner.Core.SettingsManager.Config.GravityMultiplier:F2}";
            Raylib.DrawText(gText, Constants.ScreenWidth / 2 - Raylib.MeasureText(gText, 20) / 2, 220, 20, Color.LightGray);

            string sText = $"Max Speed Multiplier: {GodsBoundaryRunner.Core.SettingsManager.Config.MaxRunSpeedMultiplier:F2}";
            Raylib.DrawText(sText, Constants.ScreenWidth / 2 - Raylib.MeasureText(sText, 20) / 2, 260, 20, Color.LightGray);

            Raylib.DrawText("Left/Right: Adjust Gravity  |  Up/Down: Adjust Max Speed", Constants.ScreenWidth / 2 - Raylib.MeasureText("Left/Right: Adjust Gravity  |  Up/Down: Adjust Max Speed", 14) / 2, 320, 14, Color.Gray);
            Raylib.DrawText("Press Escape to Resume", Constants.ScreenWidth / 2 - Raylib.MeasureText("Press Escape to Resume", 16) / 2, 360, 16, Color.White);

            // Fullscreen hint
            string fsState = Raylib.IsWindowFullscreen() ? "Fullscreen" : "Windowed";
            string fsText = $"F11: Toggle Fullscreen (Current: {fsState})";
            Raylib.DrawText(fsText, Constants.ScreenWidth / 2 - Raylib.MeasureText(fsText, 14) / 2, 392, 14, Color.LightGray);

            // Input remapping panel
            int panelW = 520;
            int panelH = 160;
            int panelX = Constants.ScreenWidth / 2 - panelW / 2;
            int panelY = 400;

            Raylib.DrawRectangleRounded(new Rectangle(panelX, panelY, panelW, panelH), 0.12f, 4, new Color(12, 12, 16, 220));
            Raylib.DrawRectangleRoundedLines(new Rectangle(panelX, panelY, panelW, panelH), 0.12f, 4, 1f, new Color(255,255,255,18));

            Raylib.DrawText("INPUT MAPPINGS (CLICK AN ACTION TO REBIND)", panelX + 18, panelY + 10, 12, Color.LightGray);

            // Draw 4 action entries
            string[] actions = new[] { "MoveRight", "MoveLeft", "Jump", "Down" };
            actionRects.Clear();
            for (int i = 0; i < actions.Length; i++)
            {
                int y = panelY + 36 + i * 28;
                var rect = new Rectangle(panelX + 12, y, panelW - 24, 24);
                actionRects[actions[i]] = rect;
                Raylib.DrawRectangleRec(rect, new Color(18, 18, 22, 200));
                Raylib.DrawRectangleLinesEx(rect, 1, new Color(255,255,255,12));

                // Get mapping text
                var cfg = GodsBoundaryRunner.Core.InputManager.GetConfig();
                var map = cfg.Mappings.Find(m => m.Action == actions[i]);
                string mapText;
                if (map == null) mapText = "(none)";
                else
                {
                    var parts = new System.Collections.Generic.List<string>();
                    parts.AddRange(map.Keys.ConvertAll(k => k.ToString()));
                    parts.AddRange(map.Buttons.ConvertAll(b => b.ToString()));
                    mapText = parts.Count == 0 ? "(none)" : string.Join(" | ", parts);
                }

                Raylib.DrawText(actions[i].ToUpper(), (int)rect.X + 8, (int)rect.Y + 3, 12, Color.White);
                Raylib.DrawText(mapText, (int)rect.X + 160, (int)rect.Y + 3, 12, Color.LightGray);
            }

            // If currently remapping, draw prominent prompt
            if (isRemapping)
            {
                string prompt = $"PRESS A KEY OR GAMEPAD BUTTON TO BIND '{remapAction.ToUpper()}' (ESC TO CANCEL)";
                Raylib.DrawText(prompt, Constants.ScreenWidth / 2 - Raylib.MeasureText(prompt, 14) / 2, panelY + panelH + 12, 14, Color.Gold);
            }
        }

        public void DrawDialogue(string text, float timer)
        {
            int boxY = Constants.ScreenHeight - 180;
            int boxH = 140;
            int boxW = 800;
            int boxX = Constants.ScreenWidth / 2 - boxW / 2;

            // Glassmorphic Dialogue Panel
            Raylib.DrawRectangleRounded(new Rectangle(boxX, boxY, boxW, boxH), 0.15f, 4, new Color(11, 10, 15, 245));
            Raylib.DrawRectangleRoundedLines(new Rectangle(boxX, boxY, boxW, boxH), 0.15f, 4, 2f, Color.Orange);

            // Neon corner decorations
            FXSystem.DrawNeonCircle(new Vector2(boxX, boxY), 4f, Color.Orange);
            FXSystem.DrawNeonCircle(new Vector2(boxX + boxW, boxY), 4f, Color.Orange);

            // Portrait Placeholder for Ra
            Raylib.DrawRectangleRounded(new Rectangle(boxX + 30, boxY + 25, 90, 90), 0.15f, 4, Color.Black);
            Raylib.DrawRectangleRoundedLines(new Rectangle(boxX + 30, boxY + 25, 90, 90), 0.15f, 4, 1.5f, Color.Orange);
            
            // Symbolic Portrait drawing (Ra Solar Crown)
            Raylib.DrawCircle(boxX + 75, boxY + 65, 22, Color.Orange);
            Raylib.DrawCircleLines(boxX + 75, boxY + 65, 30, Color.Gold);

            // Dialogue wrapped text
            Raylib.DrawText("THE SUN GOD RA SPEAKS:", boxX + 150, boxY + 30, 12, Color.Orange);
            Raylib.DrawText(text, boxX + 150, boxY + 55, 22, Color.White);

            // Automatic dismissal notice
            Raylib.DrawText($"COMMUNICATION DISENGAGING IN: {Math.Ceiling(timer)}s", boxX + 150, boxY + 95, 11, Color.Gray);
        }

        public void DrawGameOver(int finalAnkhs, bool isVictory, string levelName, float progressX)
        {
            if (isVictory)
            {
                // 1. VICTORY OVERLAY
                Raylib.DrawRectangle(0, 0, Constants.ScreenWidth, Constants.ScreenHeight, new Color(15, 5, 5, 245));

                int centerTextY = Constants.ScreenHeight / 2 - 130;
                
                Raylib.DrawText("SEASON 1 COMPLETE", Constants.ScreenWidth / 2 - Raylib.MeasureText("SEASON 1 COMPLETE", 40) / 2, centerTextY, 40, Color.Gold);
                FXSystem.DrawNeonLine(new Vector2(100, centerTextY + 65), new Vector2(Constants.ScreenWidth - 100, centerTextY + 65), 2f, Color.Gold);

                Raylib.DrawText("THE BOUNDARY HAS BEEN TRANSCENDED.", Constants.ScreenWidth / 2 - Raylib.MeasureText("THE BOUNDARY HAS BEEN TRANSCENDED.", 18) / 2, centerTextY + 90, 18, Color.White);

                // Glassmorphism collection status card
                int cardW = 460;
                int cardH = 110;
                int cardX = Constants.ScreenWidth / 2 - cardW / 2;
                int cardY = centerTextY + 140;

                Raylib.DrawRectangleRounded(new Rectangle(cardX, cardY, cardW, cardH), 0.15f, 4, new Color(20, 15, 15, 220));
                Raylib.DrawRectangleRoundedLines(new Rectangle(cardX, cardY, cardW, cardH), 0.15f, 4, 1.5f, Color.Gold);

                Raylib.DrawText("TOTAL SACRED ANKHS TRANSCENDED", cardX + 35, cardY + 25, 12, Color.LightGray);
                Raylib.DrawText($"{finalAnkhs:N0} ANKHS", cardX + 35, cardY + 45, 36, Color.Gold);

                // Restart Instruction prompt
                float pulse = 0.5f + MathF.Sin((float)Raylib.GetTime() * 4f) * 0.5f;
                Color promptColor = Color.White;
                promptColor.A = (byte)(100 + pulse * 155);

                Raylib.DrawText("PRESS 'R' TO RE-ENTER THE MORTAL BOUNDARY", Constants.ScreenWidth / 2 - Raylib.MeasureText("PRESS 'R' TO RE-ENTER THE MORTAL BOUNDARY", 14) / 2, centerTextY + 290, 14, promptColor);
            }
            else
            {
                // 2. CRASH / RUN TERMINATED OVERLAY (Restart prompted)
                // Translucent pitch-black overlay with dynamic dark crimson glow
                Raylib.DrawRectangle(0, 0, Constants.ScreenWidth, Constants.ScreenHeight, new Color(10, 2, 2, 230));

                int cY = Constants.ScreenHeight / 2 - 140;

                // Blinking RUN TERMINATED Text
                float blink = 0.6f + MathF.Sin((float)Raylib.GetTime() * 12f) * 0.4f;
                Color termColor = new Color(255, 30, 30, (int)(blink * 255));

                Raylib.DrawText("R U N   T E R M I N A T E D", Constants.ScreenWidth / 2 - Raylib.MeasureText("R U N   T E R M I N A T E D", 32) / 2, cY, 32, termColor);
                FXSystem.DrawNeonLine(new Vector2(150, cY + 55), new Vector2(Constants.ScreenWidth - 150, cY + 55), 2.5f, Color.Red);

                Raylib.DrawText("BOUNDARY COLLISION OR DETECTED INSTABILITY", Constants.ScreenWidth / 2 - Raylib.MeasureText("BOUNDARY COLLISION OR DETECTED INSTABILITY", 13) / 2, cY + 75, 13, Color.Gray);

                // Elegant Telemetry Details Card
                int boxW = 500;
                int boxH = 160;
                int boxX = Constants.ScreenWidth / 2 - boxW / 2;
                int boxY = cY + 115;

                Raylib.DrawRectangleRounded(new Rectangle(boxX, boxY, boxW, boxH), 0.15f, 4, new Color(25, 10, 10, 200));
                Raylib.DrawRectangleRoundedLines(new Rectangle(boxX, boxY, boxW, boxH), 0.15f, 4, 1.5f, Color.Red);

                // Level Name and Progress
                float progressPercent = Math.Clamp((progressX / Constants.LevelLength) * 100f, 0f, 100f);
                Raylib.DrawText("SECTOR FAILURE:", boxX + 35, boxY + 25, 11, Color.Gray);
                Raylib.DrawText(levelName.ToUpper(), boxX + 35, boxY + 40, 22, Color.White);

                // Progress Statistics
                Raylib.DrawText($"COORDINATES REACHED: {progressX:N0}m / {Constants.LevelLength:N0}m", boxX + 35, boxY + 78, 12, Color.LightGray);
                Raylib.DrawText($"ANKHS GATHERED: {finalAnkhs} ANKHS", boxX + 35, boxY + 98, 12, Color.Gold);

                // Sleek visual Progress Bar
                int barWidth = boxW - 70;
                int barHeight = 8;
                int barX = boxX + 35;
                int barY = boxY + 125;

                // Base Empty bar
                Raylib.DrawRectangle(barX, barY, barWidth, barHeight, new Color(40, 20, 20, 255));
                // Filled portion with a glowing neon red accent
                int fillWidth = (int)(barWidth * (progressPercent / 100f));
                if (fillWidth > 0)
                {
                    Raylib.DrawRectangle(barX, barY, fillWidth, barHeight, Color.Red);
                    Color barGlow = Color.Red; barGlow.A = 80;
                    Raylib.DrawRectangle(barX - 1, barY - 1, fillWidth + 2, barHeight + 2, barGlow);
                }
                Raylib.DrawText($"{progressPercent:F1}% COMPLETE", barX + barWidth - 90, barY - 16, 10, Color.Red);

                // Restart instructions
                float pulse = 0.5f + MathF.Sin((float)Raylib.GetTime() * 5.0f) * 0.5f;
                Color promptColor = Color.White;
                promptColor.A = (byte)(110 + pulse * 145);

                Raylib.DrawText("PRESS 'R' TO INITIATE SYSTEM REBUILD AND RE-ENTER RUN", Constants.ScreenWidth / 2 - Raylib.MeasureText("PRESS 'R' TO INITIATE SYSTEM REBUILD AND RE-ENTER RUN", 13) / 2, boxY + 195, 13, promptColor);
            }
        }
    }
}
