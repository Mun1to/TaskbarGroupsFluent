using System.IO;
using System.Xml.Serialization;
using TaskbarGroups.Core;
using Xunit;

public class CategoryRebuildIconTests
{
    private static Category Read(string xml)
    {
        using var reader = new StringReader(xml);
        return (Category)new XmlSerializer(typeof(Category)).Deserialize(reader)!;
    }

    [Fact] // A group saved with an image or an emoji (GridIcon false) keeps it.
    public void PickedIcon_IsNeverRebuilt()
        => Assert.False(Read("<?xml version=\"1.0\"?><Category><Name>g</Name><GridIcon>false</GridIcon></Category>").ShouldRebuildIcon);

    [Fact] // A file from before 1.6.0 has no GridIcon at all: the user's icon, untouched.
    public void FileWithoutGridIcon_IsNeverRebuilt()
        => Assert.False(Read("<?xml version=\"1.0\"?><Category><Name>g</Name></Category>").ShouldRebuildIcon);

    [Fact] // An icon the app composed from the apps follows them.
    public void GridIcon_IsRebuilt()
        => Assert.True(Read("<?xml version=\"1.0\"?><Category><Name>g</Name><GridIcon>true</GridIcon></Category>").ShouldRebuildIcon);

    [Fact] // A folder group wears the folder's own icon.
    public void FolderGroup_IsNeverRebuilt()
        => Assert.False(new Category { GridIcon = true, FolderSource = "shell:Downloads" }.ShouldRebuildIcon);
}
