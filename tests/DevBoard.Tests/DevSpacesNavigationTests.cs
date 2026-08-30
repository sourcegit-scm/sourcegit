using System.Linq;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using Xunit;

namespace DevBoard.Tests
{
    [Trait("Category", "UIIntegration")]
    public sealed class DevSpacesNavigationTests
    {
        [AvaloniaFact]
        public void RepositoryDevSpacesNavigationContainsFilesAndAIRouterChildren()
        {
            var assembly = typeof(Views.Repository).Assembly;
            var integrationType = assembly.GetType("DevBoard.DevSpaces.DevSpacesBootstrap+RepositoryIntegration");
            Assert.NotNull(integrationType);

            var factory = integrationType.GetMethod("CreateNavigationItem", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(factory);

            var arguments = new object[] { new Views.Repository(), null, null, null };
            var item = Assert.IsType<ListBoxItem>(factory.Invoke(null, arguments));
            var root = Assert.IsType<StackPanel>(item.Content);
            var tools = Assert.IsType<StackPanel>(root.Children[1]);
            var labels = tools.Children
                .OfType<Button>()
                .Select(x => Assert.IsType<StackPanel>(x.Content))
                .SelectMany(x => x.Children.OfType<TextBlock>())
                .Select(x => x.Text)
                .ToArray();

            Assert.Contains(App.Text("DevSpaces.Files"), labels);
            Assert.Contains("AI Router", labels);
        }
    }
}
