using TimerSala.Core.Info;

namespace TimerSala.Tests;

public class SetupPlanTests
{
    [Fact]
    public void Kind_compares_with_the_installed_copy()
    {
        Assert.Equal(SetupKind.Install, SetupPlan.Kind(null, "2.4.0-beta.1"));
        Assert.Equal(SetupKind.Upgrade, SetupPlan.Kind("2.3.0-beta.4", "2.4.0-beta.1"));
        Assert.Equal(SetupKind.Upgrade, SetupPlan.Kind("2.4.0-beta.1", "2.4.0"));       // dalla beta alla stabile
        Assert.Equal(SetupKind.Reinstall, SetupPlan.Kind("2.4.0-beta.1", "2.4.0-beta.1"));
        Assert.Equal(SetupKind.Downgrade, SetupPlan.Kind("2.4.0-beta.1", "1.14.0"));    // si può sempre tornare indietro
    }

    [Fact]
    public void Offered_only_from_a_downloaded_copy()
    {
        var installed = @"C:\Users\x\AppData\Local\Programs\TimerSala";
        Assert.True(SetupPlan.Offer(@"C:\Users\x\Downloads\TimerSala.exe", installed, null, devBuild: false));
        Assert.True(SetupPlan.Offer(@"C:\Users\x\Downloads\TimerSala.exe", null, null, devBuild: false));
        Assert.False(SetupPlan.Offer(installed + @"\TimerSala.exe", installed + @"\", null, devBuild: false));
        Assert.False(SetupPlan.Offer(@"C:\USERS\X\Desktop\TimerSala.exe", installed, @"C:\Users\x\Desktop\TimerSala.exe", devBuild: false));
        Assert.False(SetupPlan.Offer(@"C:\Users\x\Downloads\TimerSala.exe", installed, null, devBuild: true));
    }

    [Fact]
    public void Custom_folder_gets_its_own_TimerSala_folder()
    {
        Assert.Equal(@"D:\Programmi\TimerSala", SetupPlan.FolderFor(@"D:\Programmi"));
        Assert.Equal(@"D:\TimerSala", SetupPlan.FolderFor(@"D:\"));
        Assert.Equal(@"D:\App\TimerSala", SetupPlan.FolderFor(@"D:\App\TimerSala\"));
    }
}
