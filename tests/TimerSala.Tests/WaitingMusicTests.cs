using TimerSala.Core.Timing;

namespace TimerSala.Tests;

public class WaitingMusicTests
{
    static TimeSpan Min(double m) => TimeSpan.FromMinutes(m);

    [Fact]
    public void Starts_inside_its_window_once()
    {
        Assert.False(WaitingMusic.Due(Min(5.1), 5, done: false)); // non ancora
        Assert.True(WaitingMusic.Due(Min(5), 5, done: false));
        Assert.True(WaitingMusic.Due(Min(2), 5, done: false));    // PC acceso tardi: parte comunque
        Assert.False(WaitingMusic.Due(Min(2), 5, done: true));    // già partita
    }

    [Fact]
    public void Never_after_the_invitation_to_sit_or_when_off()
    {
        Assert.False(WaitingMusic.Due(TimeSpan.FromSeconds(30), 5, done: false));
        Assert.False(WaitingMusic.Due(TimeSpan.FromSeconds(-10), 5, done: false));
        Assert.False(WaitingMusic.Due(Min(1), 0, done: false));
    }

    [Fact]
    public void Music_and_share_can_be_apart()
    {
        // musica a 10 minuti, muto e condivisione a 5: a 7 minuti solo la musica
        Assert.True(WaitingMusic.Due(Min(7), 10, done: false));
        Assert.False(WaitingMusic.Due(Min(7), 5, done: false));
    }
}
