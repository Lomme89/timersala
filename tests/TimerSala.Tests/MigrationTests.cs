using TimerSala.Core.Storage;

namespace TimerSala.Tests;

/// <summary>Passaggio dalla 1.x: impostazioni salvate dalle versioni precedenti si leggono intatte.</summary>
public class MigrationTests
{
    // come lo scriveva la 1.13 (senza le opzioni nuove della 2.0)
    const string Settings113 = """
    {
      "MidweekDay": 4,
      "MidweekTime": "19:30:00",
      "WeekendDay": 6,
      "WeekendTime": "17:00:00",
      "MeetingLengthMinutes": 110,
      "AdaptiveStudy": true,
      "CountdownMinutes": 7,
      "CountdownStyle": "Ring",
      "DisplayTheme": "Light",
      "DisplayLayout": "Hourglass",
      "DisplayFont": "Arial",
      "ShowProgressBar": false,
      "MessagesEnabled": true,
      "MessagePresets": [ "Concludi", "Grazie" ],
      "MessageSeconds": 20,
      "MessageFullScreenSeconds": 8,
      "RemoteControlEnabled": true,
      "RemotePin": "4321",
      "VoiceStartEnabled": true,
      "VoiceThresholdDb": -35,
      "MiniMode": true,
      "MiniLeft": 1200,
      "MiniTop": 700,
      "OverseerVisits": [ "2026-11-09", "2027-03-15" ],
      "OverseerMidweekDay": 2,
      "OverseerMidweekTime": "19:00:00",
      "WolCode": "en",
      "WolRsconf": "r1",
      "WolLib": "lp-e",
      "TimerMonitor": "\\\\.\\DISPLAY3",
      "TimerWindowVisible": true,
      "WebServerEnabled": true,
      "WebServerPort": 8091,
      "WebAddressMode": "hostname",
      "WarningSeconds": 90,
      "ControllerTopmost": true,
      "ControllerLeft": 100,
      "ControllerTop": 50,
      "ControllerWidth": 460,
      "ControllerHeight": 820
    }
    """;

    [Fact]
    public void Settings_from_1_13_keep_every_value()
    {
        var dir = Path.Combine(Path.GetTempPath(), "timersala-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DataStore(dir);
            File.WriteAllText(Path.Combine(dir, "impostazioni.json"), Settings113);
            var s = store.LoadSettings();

            Assert.Equal(DayOfWeek.Thursday, s.MidweekDay);
            Assert.Equal(new TimeOnly(19, 30), s.MidweekTime);
            Assert.Equal(DayOfWeek.Saturday, s.WeekendDay);
            Assert.Equal(110, s.MeetingLengthMinutes);
            Assert.True(s.AdaptiveStudy);
            Assert.Equal(CountdownStyle.Ring, s.CountdownStyle);
            Assert.Equal(DisplayTheme.Light, s.DisplayTheme);
            Assert.Equal(DisplayLayout.Hourglass, s.DisplayLayout);
            Assert.False(s.ShowProgressBar);
            Assert.Equal(["Concludi", "Grazie"], s.MessagePresets);
            Assert.Equal("4321", s.RemotePin);
            Assert.Equal(-35, s.VoiceThresholdDb);
            Assert.Equal([new DateOnly(2026, 11, 9), new DateOnly(2027, 3, 15)], s.OverseerVisits);
            Assert.Equal(DayOfWeek.Tuesday, s.OverseerMidweekDay);
            Assert.Equal("en", s.Language.Code);
            Assert.Equal(@"\\.\DISPLAY3", s.TimerMonitor);
            Assert.Equal(8091, s.WebServerPort);
            Assert.Equal(90, s.WarningSeconds);
            Assert.Equal(460, s.ControllerWidth);

            // opzioni nuove: valori predefiniti, nessuna domanda all'utente
            Assert.Equal(ControllerTheme.Dark, s.ControllerTheme);
            Assert.Equal(100, s.UiScalePercent);
            Assert.Empty(s.Windows);

            // salvate dalla 2.0 e rilette, restano uguali
            store.SaveSettings(s);
            var again = store.LoadSettings();
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(s), System.Text.Json.JsonSerializer.Serialize(again));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
