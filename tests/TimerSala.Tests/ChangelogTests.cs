using TimerSala.Core.Info;

namespace TimerSala.Tests;

public class ChangelogTests
{
    static string RepoChangelog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CHANGELOG.md"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "CHANGELOG.md"));
    }

    [Fact]
    public void Versions_compare_with_previews_before_stable()
    {
        Assert.True(SemVer.Compare("2.0.0-beta.1", "1.14.0") > 0);
        Assert.True(SemVer.Compare("2.0.0-beta.2", "2.0.0-beta.1") > 0);
        Assert.True(SemVer.Compare("2.0.0-beta.10", "2.0.0-beta.9") > 0);
        Assert.True(SemVer.Compare("2.0.0", "2.0.0-beta.3") > 0);
        Assert.Equal(0, SemVer.Compare("v1.13.2", "1.13.2"));
    }

    [Fact]
    public void The_real_changelog_is_read_and_filtered()
    {
        var entries = Changelog.Parse(RepoChangelog());
        Assert.Contains(entries, e => e.Version == "1.13.2");
        Assert.Contains(entries, e => e.Version == "1.0.0");
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Body)));

        var news = Changelog.Since(entries, "1.13.0", "1.13.2");
        Assert.Equal(["1.13.2", "1.13.1"], news.Select(e => e.Version));
        Assert.Single(Changelog.Since(entries, null, "1.13.2"));
    }
}
