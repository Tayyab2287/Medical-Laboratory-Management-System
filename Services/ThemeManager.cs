using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace LabReportApp.Services
{
    public static class ThemeManager
    {
        // Remembers the chosen theme in a small text file next to the .exe, so it's
        // still applied the next time the app is opened.
        private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.txt");

        public static string CurrentTheme { get; private set; } = "Light";

        /// <summary>Reads the saved theme preference (if any) and applies it. Call once at startup.</summary>
        public static void LoadSavedTheme()
        {
            string theme = "Light";

            try
            {
                if (File.Exists(SettingsFile))
                {
                    string saved = File.ReadAllText(SettingsFile).Trim();
                    if (saved.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                        theme = "Dark";
                }
            }
            catch
            {
                // If the file can't be read for any reason, just fall back to Light.
            }

            ApplyTheme(theme);
        }

        /// <summary>Switches to the other theme (Light -> Dark or Dark -> Light).</summary>
        public static void ToggleTheme()
        {
            ApplyTheme(CurrentTheme == "Dark" ? "Light" : "Dark");
        }

        /// <summary>Applies the given theme ("Light" or "Dark") immediately across the whole app.</summary>
        public static void ApplyTheme(string themeName)
        {
            string fileName = themeName == "Dark" ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";

            var newDictionary = new ResourceDictionary
            {
                Source = new Uri(fileName, UriKind.Relative)
            };

            var appResources = Application.Current.Resources;

            // Remove whichever theme dictionary is currently merged in (identified by it having
            // "ColorPrimary" defined and coming from a "Themes/..." source), then add the new one.
            // Every other resource (styles, etc.) defined directly in App.xaml is left untouched.
            var oldThemeDictionaries = appResources.MergedDictionaries
                .Where(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"))
                .ToList();

            foreach (var old in oldThemeDictionaries)
                appResources.MergedDictionaries.Remove(old);

            appResources.MergedDictionaries.Add(newDictionary);
            CurrentTheme = themeName;

            try
            {
                File.WriteAllText(SettingsFile, themeName);
            }
            catch
            {
                // Not being able to save the preference isn't critical - the app still works,
                // it'll just default back to Light next time it's opened.
            }
        }
    }
}