using TimerSala.Core.Input;

namespace TimerSala.Tests;

public class ClickerKeysTests
{
    [Fact]
    public void Page_down_starts_or_stops_and_page_up_is_always_undo()
    {
        Assert.Equal(ClickerAction.Forward, ClickerKeys.FromKeyboard(0x22));
        Assert.Equal(ClickerAction.Back, ClickerKeys.FromKeyboard(0x21));
        Assert.Equal(ClickerAction.None, ClickerKeys.FromKeyboard(0x27)); // le frecce della tastiera scelgono la parte
        Assert.Equal(ClickerAction.Forward, ClickerKeys.FromAssociatedDevice(0x27)); // dal telecomando sono «avanti»
        Assert.Equal(ClickerAction.Back, ClickerKeys.FromAssociatedDevice(0x25));
        Assert.Equal(ClickerAction.None, ClickerKeys.FromAssociatedDevice(0x42)); // «B», schermo nero: ignorato
    }

    [Fact]
    public void A_real_keyboard_is_recognized_by_its_letters()
    {
        Assert.True(ClickerKeys.IsTypingKey('A'));
        Assert.True(ClickerKeys.IsTypingKey('5'));
        Assert.False(ClickerKeys.IsTypingKey('B'));   // schermo nero dei telecomandi
        Assert.False(ClickerKeys.IsTypingKey(0x22));
        Assert.False(ClickerKeys.IsTypingKey(0x74));  // F5
    }

    [Fact]
    public void Device_names_are_short()
    {
        Assert.Equal("VID 046D · PID C52B", ClickerKeys.ShortName(@"\\?\HID#VID_046D&PID_C52B&MI_00#7&2c0a2d6&0&0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}"));
        Assert.Equal("dispositivo USB", ClickerKeys.ShortName("qualcosa"));
    }
}
