using TaskbarGroups.Core;
using Xunit;

public class UiPreferencesLanguageTests
{
    [Theory]
    [InlineData("zh-Hans", "zh-Hans")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-SG", "zh-Hans")]
    [InlineData("zh-Hans-CN", "zh-Hans")]
    [InlineData("zh-Hant", "en")]
    [InlineData("zh-TW", "en")]
    [InlineData("zh-HK", "en")]
    [InlineData("es-MX", "es")]
    [InlineData("ru-RU", "ru")]
    [InlineData("en-US", "en")]
    public void ResolvesTheSupportedDisplayLanguage(string culture, string expected)
    {
        Assert.Equal(expected, UiPreferences.Known(culture));
    }
}
