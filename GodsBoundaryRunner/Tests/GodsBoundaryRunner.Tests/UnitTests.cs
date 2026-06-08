using Xunit;
using GodsBoundaryRunner.Core;
using GodsBoundaryRunner.Systems;
using System.Threading.Tasks;
using System.Threading;

public class UnitTests
{
    [Fact]
    public void Settings_Defaults_AreReasonable()
    {
        SettingsManager.Load();
        var cfg = SettingsManager.Config;
        Assert.InRange(cfg.GravityMultiplier, 0.1f, 10f);
        Assert.InRange(cfg.MaxRunSpeedMultiplier, 0.1f, 10f);
        Assert.InRange(cfg.MasterVolume, 0f, 1f);
    }

    [Fact]
    public void LevelGeneration_IsDeterministic()
    {
        var lm1 = new LevelManager();
        lm1.LoadLevel(LevelID.Tehuti);
        int c1 = lm1.Obstacles.Count + lm1.Ankhs.Count;

        var lm2 = new LevelManager();
        lm2.LoadLevel(LevelID.Tehuti);
        int c2 = lm2.Obstacles.Count + lm2.Ankhs.Count;

        Assert.Equal(c1, c2);
    }

    

    [Fact]
    public async Task LevelGeneration_CanBeCancelled()
    {
        var lm = new LevelManager();
        lm.StartLoadLevel(LevelID.Geb);
        // cancel immediately
        lm.CancelLoad();

        // wait up to 1s for the background task to observe cancellation
        int waited = 0;
        while (lm.IsLoading && waited < 1000)
        {
            await Task.Delay(50);
            waited += 50;
        }

        Assert.False(lm.IsLoading);
        Assert.Equal(0f, lm.LoadProgress);
    }
}
