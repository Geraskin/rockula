using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace RockULA.Desktop;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "RockULA! - Rock Your Spectrum.",
                Width = 760,
                Height = 460,
                MinWidth = 480,
                MinHeight = 320,
                Content = new StackPanel
                {
                    Margin = new Thickness(32),
                    Spacing = 18,
                    Children =
                    {
                        new TextBlock { Text = "RockULA!", FontSize = 44 },
                        new TextBlock { Text = "Rock Your Spectrum.", FontSize = 22 },
                        new TextBlock
                        {
                            Text = "ZX Spectrum emulator written from scratch in C#.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "CPU loads, byte ALU, branches and stack. Spectrum devices and game loading are planned.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "The headless --demo command runs a small Z80 program without firmware.",
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
