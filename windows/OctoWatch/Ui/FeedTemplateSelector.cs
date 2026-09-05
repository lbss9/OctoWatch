using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OctoWatch;

/// <summary>Pull-request cards use an expandable template; everything else stays flat.</summary>
public sealed partial class FeedTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Simple { get; set; }
    public DataTemplate? Pull { get; set; }

    protected override DataTemplate SelectTemplateCore(object item) =>
        item is FeedItem feed && feed.Kind == FeedMapper.KindPull ? Pull! : Simple!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}
