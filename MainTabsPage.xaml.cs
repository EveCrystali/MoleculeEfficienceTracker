using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MoleculeEfficienceTracker
{
    public partial class MainTabsPage : TabbedPage
    {
        private readonly Dictionary<Page, (Func<Page> factory, ImageSource? icon)> _lazyPages = new();

        public MainTabsPage()
        {
            InitializeComponent();

            AddLazyTab("🍵", () => new CaffeinePage());
            AddLazyTab("🧠", () => new BromazepamPage());
            AddLazyTab("🩹", () => new PainReliefPage());
            AddLazyTab("🍾", () => new AlcoholPage());
            AddLazyTab("⚙️", () => new SettingsPage());

            CurrentPageChanged += async (s, e) => await LoadLazyPageAsync(CurrentPage);

            // Preload the first tab for a smoother startup
            if (Children.Count > 0)
                _ = LoadLazyPageAsync(Children[0]);
        }

        private void AddLazyTab(string title, Func<Page> factory)
        {
            var placeholder = new ContentPage { Title = title };
            _lazyPages[placeholder] = (factory, placeholder.IconImageSource);
            Children.Add(placeholder);
        }

        private async Task LoadLazyPageAsync(Page placeholder)
        {
            if (!_lazyPages.TryGetValue(placeholder, out var info))
                return;

            _lazyPages.Remove(placeholder);

            await Task.Yield(); // allow UI to render before heavy load
            var page = info.factory();
            page.Title = placeholder.Title;
            page.IconImageSource = info.icon;
            var index = Children.IndexOf(placeholder);
            Children[index] = page;
        }
    }
}
