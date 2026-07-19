using System.Xml.Linq;

namespace TasksList.App.Tests.Shell;

public sealed class WindowsAccessibilityContractTests
{
    [Theory]
    [InlineData("MainWindow.xaml", "SearchBox", "Search notes")]
    [InlineData("MainWindow.xaml", "NotesList", "Working notes")]
    [InlineData("MainWindow.xaml", "PlacesList", "Places")]
    [InlineData("MainWindow.xaml", "MainTabs", "Task'sList library sections")]
    [InlineData("Clipboard/ClipboardPaletteWindow.xaml", "TypeFilter", "Filter by type")]
    [InlineData("Clipboard/ClipboardPaletteWindow.xaml", "DateFilter", "Filter by date")]
    [InlineData("Clipboard/ClipboardPaletteWindow.xaml", "SourceFilter", "Filter by source application")]
    [InlineData("Clipboard/ClipboardPaletteWindow.xaml", "ClipList", "Clipboard history")]
    [InlineData("Clipboard/ClipboardPaletteWindow.xaml", "PreviewText", "Clipboard item preview")]
    [InlineData("Clipboard/ClipboardPaletteWindow.xaml", "PlaceBox", "Assign to place")]
    [InlineData("Settings/SettingsWindow.xaml", "SnapSlider", "Sticky snap tolerance")]
    [InlineData("Settings/SettingsWindow.xaml", "ExcludedAppsBox", "Excluded clipboard applications")]
    [InlineData("Places/NewPlaceDialog.xaml", "NameBox", "Place name")]
    [InlineData("Places/NewPlaceDialog.xaml", "ParentBox", "Parent place")]
    [InlineData("Sticky/ScheduleDialog.xaml", "DateBox", "Reminder date")]
    [InlineData("Sticky/ScheduleDialog.xaml", "TimeBox", "Reminder time")]
    [InlineData("Sticky/StickyWindow.xaml", "TitleEditor", "Note title")]
    [InlineData("Sticky/StickyWindow.xaml", "MarkdownBox", "Note Markdown")]
    [InlineData("Sticky/StickyWindow.xaml", "ModeButton", "Toggle edit and preview")]
    [InlineData("Clipboard/ClipboardEditDialog.xaml", "TitleBox", "Clipboard item title")]
    [InlineData("Clipboard/ClipboardEditDialog.xaml", "ContentBox", "Clipboard item content")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "BackgroundColorBox", "Paper background color")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "TextColorBox", "Note text color")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "AccentColorBox", "Note accent color")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "FontFamilyBox", "Note font family")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "FontSizeBox", "Note font size")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "FontWeightBox", "Note font weight")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "LineSpacingBox", "Note line spacing")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "DensityBox", "Note density")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "ToolbarBox", "Toolbar visibility")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "ShadowSlider", "Note shadow strength")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "CornerBox", "Note corner style")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "NamedStyleBox", "Saved note style")]
    [InlineData("Sticky/CustomizeFlyout.xaml", "StyleNameBox", "New style name")]
    public void ImportantControlsExposeStableAutomationNames(string relativePath, string elementName, string expectedName)
    {
        var element = FindNamedElement(relativePath, elementName);

        Assert.Equal(expectedName, Attribute(element, "AutomationProperties.Name"));
    }

    [Theory]
    [InlineData("MainWindow.xaml", "AppNotificationHost", "Polite")]
    [InlineData("Settings/SettingsWindow.xaml", "ErrorText", "Assertive")]
    [InlineData("Sticky/ScheduleDialog.xaml", "ErrorText", "Assertive")]
    [InlineData("Sticky/StickyWindow.xaml", "ReminderBanner", "Assertive")]
    public void DynamicFeedbackUsesAutomationLiveRegions(string relativePath, string elementName, string expectedSetting)
    {
        var element = FindNamedElement(relativePath, elementName);

        Assert.Equal(expectedSetting, Attribute(element, "AutomationProperties.LiveSetting"));
    }

    [Fact]
    public void MainNavigationTabsHaveExplicitNames()
    {
        var document = Load("MainWindow.xaml");
        var tabs = document.Descendants().Where(element => element.Name.LocalName == "TabItem").ToArray();

        Assert.Equal(5, tabs.Length);
        Assert.All(tabs, tab => Assert.False(string.IsNullOrWhiteSpace(Attribute(tab, "AutomationProperties.Name"))));
    }

    [Fact]
    public void SettingsAndScheduleUseNativeDwmTreatment()
    {
        var root = FindRepositoryRoot();
        foreach (var sourcePath in new[]
                 {
                     Path.Combine(root, "src", "TasksList.App", "Settings", "SettingsWindow.xaml.cs"),
                     Path.Combine(root, "src", "TasksList.App", "Sticky", "ScheduleDialog.xaml.cs"),
                 })
        {
            var source = File.ReadAllText(sourcePath);
            Assert.Contains("DwmWindowService.Apply", source, StringComparison.Ordinal);
            Assert.Contains("DwmWindowKind.Transient", source, StringComparison.Ordinal);
        }
    }

    private static XElement FindNamedElement(string relativePath, string name)
    {
        var document = Load(relativePath);
        return Assert.Single(document.Descendants().Where(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == name)));
    }

    private static XDocument Load(string relativePath) => XDocument.Load(Path.Combine(
        FindRepositoryRoot(), "src", "TasksList.App", relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string? Attribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TasksList.sln"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Task'sList repository root.");
    }
}
