using System;
using Raylib_cs;
using System.Runtime.InteropServices;
using GodsBoundaryRunner.Core;

namespace GodsBoundaryRunner.Systems
{
    public class SoundManager
    {
        private static readonly Random rng = System.Random.Shared;
        private Sound jumpSound;
        private Sound slideSound;
        private Sound collectSound;
        private Sound stumbleSound;
        private Sound gateSound;
        private bool isAudioReady = false;
        private readonly System.Collections.Generic.Dictionary<string, double> lastPlayed = new System.Collections.Generic.Dictionary<string, double>();
        private readonly System.Collections.Generic.Dictionary<string, double> minInterval = new System.Collections.Generic.Dictionary<string, double>
        {
            { "jump", 0.06 },
            { "slide", 0.08 },
            { "collect", 0.04 },
            { "stumble", 0.2 },
            { "gate", 0.4 }
        };
        // Keep track of unmanaged buffers allocated for Waves so we can free them on dispose
        private readonly System.Collections.Generic.List<IntPtr> allocatedBuffers = new System.Collections.Generic.List<IntPtr>();

        public void Initialize()
        {
            try
            {
                Raylib.InitAudioDevice();
                if (Raylib.IsAudioDeviceReady())
                {
                    isAudioReady = true;
                    // Apply persisted master volume
                    Raylib.SetMasterVolume(SettingsManager.Config.MasterVolume);
                    GenerateProceduralSounds();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Audio System initialization failed. Running in silent mode.", ex);
                isAudioReady = false;
            }
        }

        private void GenerateProceduralSounds()
        {
            // 1. Jump Sound (Rising Sine Sweep)
            jumpSound = GenerateSynthSound(0.12f, 44100, (t) => {
                float freq = 200f + t * 900f; // Sweep from 200Hz to 1100Hz
                return MathF.Sin(2f * MathF.PI * freq * t) * (1f - t); // Decay envelope
            });

            // 2. Slide Sound (Low Pitch Friction)
            slideSound = GenerateSynthSound(0.25f, 44100, (t) => {
                float freq = 120f + MathF.Sin(t * 30f) * 20f;
                float noise = (float)(rng.NextDouble() * 2f - 1f) * 0.15f;
                return (MathF.Sin(2f * MathF.PI * freq * t) + noise) * (1f - t);
            });

            // 3. Collect Sound (High Crisp Double Chime)
            collectSound = GenerateSynthSound(0.15f, 44100, (t) => {
                float freq = (t < 0.07f) ? 880f : 1320f; // E5 then E6
                return MathF.Sin(2f * MathF.PI * freq * t) * 0.4f * (1f - t / 0.15f);
            });

            // 4. Stumble Sound (Impact Noise Crash)
            stumbleSound = GenerateSynthSound(0.35f, 44100, (t) => {
                float noise = (float)(rng.NextDouble() * 2f - 1f) * 0.4f;
                float bass = MathF.Sin(2f * MathF.PI * 65f * t) * 0.4f;
                return (noise + bass) * (1f - t);
            });

            // 5. Gate Sound (Major Triad Sweep)
            gateSound = GenerateSynthSound(0.6f, 44100, (t) => {
                float freq = 440f;
                if (t > 0.15f && t <= 0.3f) freq = 554.37f; // C#
                else if (t > 0.3f && t <= 0.45f) freq = 659.25f; // E
                else if (t > 0.45f) freq = 880f; // A
                return MathF.Sin(2f * MathF.PI * freq * t) * 0.5f * (1f - t);
            });
        }

        private unsafe Sound GenerateSynthSound(float duration, int sampleRate, Func<float, float> waveFunc)
        {
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float val = waveFunc(t);
                val = Math.Clamp(val, -1f, 1f);
                samples[i] = (short)(val * 32767);
            }

            // Allocate unmanaged buffer and copy samples so the data remains valid after the method returns
            IntPtr buffer = IntPtr.Zero;
            try
            {
                int byteCount = totalSamples * sizeof(short);
                buffer = Marshal.AllocHGlobal(byteCount);
                Marshal.Copy(samples, 0, buffer, totalSamples);

                Wave wave = new Wave
                {
                    SampleCount = (uint)totalSamples,
                    SampleRate = (uint)sampleRate,
                    SampleSize = 16,
                    Channels = 1,
                    Data = buffer.ToPointer()
                };

                allocatedBuffers.Add(buffer);

                return Raylib.LoadSoundFromWave(wave);
            }
            catch (Exception ex)
            {
                if (buffer != IntPtr.Zero)
                {
                    try { Marshal.FreeHGlobal(buffer); } catch { }
                }
                Logger.Log("Failed to generate synth sound", ex);
                return default;
            }
        }

        private bool CanPlay(string key)
        {
            double now = Raylib.GetTime();
            if (!minInterval.ContainsKey(key)) return true;
            double min = minInterval[key];
            if (!lastPlayed.TryGetValue(key, out var last) || (now - last) >= min)
            {
                lastPlayed[key] = now;
                return true;
            }
            return false;
        }

        public void PlayJump() { if (isAudioReady && CanPlay("jump")) Raylib.PlaySound(jumpSound); }
        public void PlaySlide() { if (isAudioReady && CanPlay("slide")) Raylib.PlaySound(slideSound); }
        public void PlayCollect() { if (isAudioReady && CanPlay("collect")) Raylib.PlaySound(collectSound); }
        public void PlayStumble() { if (isAudioReady && CanPlay("stumble")) Raylib.PlaySound(stumbleSound); }
        public void PlayGate() { if (isAudioReady && CanPlay("gate")) Raylib.PlaySound(gateSound); }

        public void Dispose()
        {
            if (isAudioReady)
            {
                try
                {
                    Raylib.UnloadSound(jumpSound);
                    Raylib.UnloadSound(slideSound);
                    Raylib.UnloadSound(collectSound);
                    Raylib.UnloadSound(stumbleSound);
                    Raylib.UnloadSound(gateSound);
                    Raylib.CloseAudioDevice();

                    // Free any unmanaged buffers we allocated
                    foreach (var ptr in allocatedBuffers)
                    {
                        try { Marshal.FreeHGlobal(ptr); } catch { }
                    }
                    allocatedBuffers.Clear();
                }
                catch (Exception ex)
                {
                    Logger.Log("Error during audio disposal", ex);
                }
            }
        }
    }
}
