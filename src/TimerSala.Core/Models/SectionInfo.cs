namespace TimerSala.Core.Models;

public static class SectionInfo
{
    public static string Label(PartSection s) => s switch
    {
        PartSection.Opening => "Apertura",
        PartSection.Treasures => "Tesori della Parola di Dio",
        PartSection.Ministry => "Efficaci nel ministero",
        PartSection.Living => "Vita cristiana",
        PartSection.PublicTalk => "Discorso pubblico",
        PartSection.Watchtower => "Studio Torre di Guardia",
        PartSection.Closing => "Conclusione",
        _ => "Altro",
    };

    /// <summary>Colori delle sezioni come nella guida (grigio, verde acqua, oro, rosso).</summary>
    public static string Color(PartSection s) => s switch
    {
        PartSection.Treasures => "#3D8FA3",
        PartSection.Ministry => "#D4A017",
        PartSection.Living => "#C0413A",
        PartSection.PublicTalk => "#5B7BD5",
        PartSection.Watchtower => "#5B7BD5",
        _ => "#7A808C",
    };

    public static bool TryParse(string? s, out PartSection section) => Enum.TryParse(s, out section);
}
