using System.IO;
using System.Xml.Serialization;
using TaskbarGroups.Core;
using Xunit;

public class CategoryIconOriginTests
{
    private static Category RoundTrip(Category category)
    {
        var serializer = new XmlSerializer(typeof(Category));
        using var stream = new MemoryStream();
        serializer.Serialize(stream, category);
        stream.Position = 0;
        return (Category)serializer.Deserialize(stream)!;
    }

    [Fact]
    public void Origin_SurvivesTheXmlRoundTrip()
        => Assert.Equal(IconOrigin.Manual,
            RoundTrip(new Category { Name = "g", GroupIconOrigin = IconOrigin.Manual }).GroupIconOrigin);

    [Fact] // Every group written before this field existed.
    public void FileWithoutTheField_ReadsAsUnset()
    {
        const string xml = "<?xml version=\"1.0\"?><Category><Name>g</Name><FolderSource></FolderSource></Category>";
        using var reader = new StringReader(xml);
        var category = (Category)new XmlSerializer(typeof(Category)).Deserialize(reader)!;
        Assert.Equal(IconOrigin.Unset, category.GroupIconOrigin);
    }

    [Theory]
    [InlineData(IconOrigin.Unset, true)]
    [InlineData(IconOrigin.Auto, true)]
    [InlineData(IconOrigin.Manual, false)]
    public void ShouldRebuildIcon_OnlyForIconsTheUserDidNotPick(IconOrigin origin, bool expected)
        => Assert.Equal(expected, new Category { GroupIconOrigin = origin }.ShouldRebuildIcon);

    [Fact] // Review Focus 4.
    public void ShouldRebuildIcon_IsFalseForAFolderGroup()
        => Assert.False(new Category
        {
            GroupIconOrigin = IconOrigin.Unset,
            FolderSource = "shell:Downloads"
        }.ShouldRebuildIcon);

    [Fact] // The grid button is a choice of its own and wins.
    public void OriginToSave_GridIconIsAuto()
        => Assert.Equal(IconOrigin.Auto,
            new Category { GroupIconOrigin = IconOrigin.Manual }.OriginToSave(gridIcon: true, manualIcon: true));

    [Fact]
    public void OriginToSave_PickedIconIsManual()
        => Assert.Equal(IconOrigin.Manual,
            new Category { GroupIconOrigin = IconOrigin.Unset }.OriginToSave(gridIcon: false, manualIcon: true));

    [Fact] // Nothing chosen in this visit: keep what the file said.
    public void OriginToSave_KeepsTheLoadedValue()
        => Assert.Equal(IconOrigin.Auto,
            new Category { GroupIconOrigin = IconOrigin.Auto }.OriginToSave(gridIcon: false, manualIcon: false));
}
