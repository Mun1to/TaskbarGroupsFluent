using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskbarGroups.Core;
using Xunit;

public class HoverTraceTests
{
    private static readonly DateTime At = new(2026, 11, 24, 9, 5, 7, 250);

    private static (HoverTrace trace, List<string> lines) Make()
    {
        var lines = new List<string>();
        return (new HoverTrace(lines.Add, () => At), lines);
    }

    [Fact]
    public void WritesTheTimeTheTopicAndTheText()
    {
        var (trace, lines) = Make();
        trace.Note("pins", "2 of 9 pinned shortcuts are groups: Web, Tools");
        Assert.Equal(new[] { "2026-11-24 09:05:07.250 pins: 2 of 9 pinned shortcuts are groups: Web, Tools" }, lines);
    }

    [Fact]
    public void ANoteThatRepeatsIsNotWrittenAgain()
    {
        // The watcher looks ten times a second: resting on one icon must not fill the file.
        var (trace, lines) = Make();
        Assert.True(trace.Note("cursor", "buttons under=MSTaskSwWClass group=Web"));
        for (int i = 0; i < 50; i++)
            Assert.False(trace.Note("cursor", "buttons under=MSTaskSwWClass group=Web"));
        Assert.Single(lines);
    }

    [Fact]
    public void AChangedNoteIsWritten_AndTopicsAreKeptApart()
    {
        var (trace, lines) = Make();
        trace.Note("cursor", "group=Web");
        trace.Note("buttons", "group=Web");
        trace.Note("cursor", "group=-");
        trace.Note("cursor", "group=Web");
        Assert.Equal(4, lines.Count);
    }

    [Fact]
    public void ForgetLetsTheSameNoteThroughOnceMore()
    {
        var (trace, lines) = Make();
        trace.Note("held", "Web: a mouse button went down on it");
        trace.Forget("held");
        trace.Note("held", "Web: a mouse button went down on it");
        trace.Note("held", "Web: a mouse button went down on it");
        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void AnEventIsWrittenEveryTime()
    {
        var (trace, lines) = Make();
        trace.Event("open", "Web: shown by the panel kept loaded");
        trace.Event("open", "Web: shown by the panel kept loaded");
        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void AWriterThatThrowsIsSwallowed()
    {
        var trace = new HoverTrace(_ => throw new IOException("disk full"), () => At);
        trace.Note("pins", "x");
        trace.Event("open", "y");
    }

    [Fact]
    public void TheFileKeepsOnlyItsLastLines()
    {
        string dir = Path.Combine(Path.GetTempPath(), "tbgf-hovertrace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "hover.txt");
            for (int i = 1; i <= 12; i++) HoverTrace.Append(path, "line " + i, keep: 5);
            Assert.Equal(new[] { "line 8", "line 9", "line 10", "line 11", "line 12" }, File.ReadAllLines(path));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void TheFileSitsInTheTempFolderBesideTheOpenTimes()
    {
        Assert.Equal(Path.GetDirectoryName(OpenTimes.FilePath), Path.GetDirectoryName(HoverTrace.FilePath));
        Assert.Equal("TaskbarGroupsFluent-hover.txt", Path.GetFileName(HoverTrace.FilePath));
    }
}
