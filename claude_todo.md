display IDE-style syntax-colored code inside an Avalonia app, use AvaloniaEdit paired with TextMateSharp—this uses the exact same grammar and color themes (Dark+, Light+, Monokai, etc.) as Visual Studio Code.
Here is the complete setup to get styled, colored code snippets working.

1. Install the Packages

Run these commands in your project folder:
        bash
        dotnet add package Avalonia.AvaloniaEdit
        dotnet add package AvaloniaEdit.TextMate
        dotnet add package TextMateSharp.Grammars
        Use code with caution.

2. Include AvaloniaEdit Styles in App.axaml

Crucial step: AvaloniaEdit will not render properly unless its default styles are included in your App.axaml.
xml

        <Application xmlns="https://github.com"
        xmlns:x="http://microsoft.com"
        x:Class="MyApp.App">
        <Application.Styles>
        <FluentTheme />
        <!-- Add this line for AvaloniaEdit styling -->
        <StyleInclude Source="avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml" />
        </Application.Styles>
        </Application>

Use code with caution.
(Note: If you use SimpleTheme instead of FluentTheme, change Fluent to Simple in the StyleInclude path.)

3. Add a Styled Container in Your View (MainWindow.axaml)

Wrap TextEditor in a Border to give it rounded corners, padding, and an IDE-like background color (#1E1E1E matches VS Code's Dark+ background):

        xml
        <Window xmlns="https://github.com"
        xmlns:x="http://microsoft.com"
        xmlns:ae="clr-namespace:AvaloniaEdit;assembly=AvaloniaEdit"
        x:Class="MyApp.MainWindow"
        Width="700" Height="450">
        <Border Background="#1E1E1E"
                BorderBrush="#333333"
                BorderThickness="1"
                CornerRadius="8"
                Padding="12"
                Margin="20">
            <ae:TextEditor Name="CodeEditor"
                           IsReadOnly="True"
                           ShowLineNumbers="True"
                           Background="Transparent"
                           Foreground="#D4D4D4"
                           FontFamily="Cascadia Code, Consolas, Menlo, monospace"
                           FontSize="14"
                           HorizontalScrollBarVisibility="Auto"
                           VerticalScrollBarVisibility="Auto" />
        </Border>
    </Window>

Use code with caution.

4. Apply IDE Colors & Language Grammar (MainWindow.axaml.cs)

Initialize TextMate in your code-behind to apply the IDE color theme and language rules:
csharp
using Avalonia.Controls;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

        namespace MyApp;
        
        public partial class MainWindow : Window
        {
        public MainWindow()
        {
        InitializeComponent();

        // 1. Choose an IDE theme (DarkPlus, LightPlus, Monokai, SolarizedDark, etc.)
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);

        // 2. Attach TextMate to the editor
        var textMateInstallation = CodeEditor.InstallTextMate(registryOptions);

        // 3. Set the language by file extension (".cs", ".xml", ".json", ".py", ".js", etc.)
        string languageScope = registryOptions.GetScopeByLanguageId(
            registryOptions.GetLanguageByExtension(".cs").Id);
        textMateInstallation.SetGrammar(languageScope);

        // 4. Set your code text
        CodeEditor.Text = """
            using System;

            public class Greeter
            {
                public static void Main()
                {
                    var message = "Hello, Avalonia!";
                    Console.WriteLine(message);
                }
            }
            """;
    }
}
Use code with caution.

Common Customizations

• Switch Themes Dynamically: Call textMateInstallation.SetTheme(registryOptions.LoadTheme(ThemeName.LightPlus)); (and update your Border background to #FFFFFF for light mode).
• Hide Extra Visual Clutter: If you want a cleaner snippet card without line numbers or active-line borders, set ShowLineNumbers="False" and in code-behind set CodeEditor.Options.HighlightCurrentLine = false;.