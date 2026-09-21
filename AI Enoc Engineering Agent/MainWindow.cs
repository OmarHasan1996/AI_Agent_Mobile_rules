using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace AI_Enoc_Engineering_Agent;

public sealed class MainWindow : Window
{
    private readonly IRuleCatalogService _catalogService;
    private readonly IContractService _contractService;
    private readonly TextBox _taskInput = new() { Watermark = "Example: Create a secure employee login feature", MinHeight = 42 };
    private readonly ComboBox _platformInput = CreateComboBox("android", "ios", "multiplatform");
    private readonly TextBox _languageInput = new() { IsReadOnly = true, Text = "Kotlin", MinHeight = 42 };
    private readonly ComboBox _environmentInput = CreateComboBox("dev", "qa", "production");
    private readonly ComboBox _formatInput = CreateComboBox("markdown", "json");
    private readonly StackPanel _rulesList = new() { Spacing = 4 };
    private readonly TextBox _output = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Cascadia Mono, Menlo, monospace")
    };
    private readonly TextBlock _status = new() { Foreground = Brushes.Gray };
    private RuleCatalog? _catalog;

    public MainWindow(IRuleCatalogService? catalogService = null, IContractService? contractService = null)
    {
        _catalogService = catalogService ?? new RuleCatalogService();
        _contractService = contractService ?? new ContractService();
        Title = "ENOC Engineering Contract Generator";
        Width = 1160;
        Height = 820;
        MinWidth = 820;
        MinHeight = 620;
        Background = new SolidColorBrush(Color.Parse("#F7F8FA"));
        _platformInput.SelectionChanged += (_, _) =>
        {
            UpdateLanguage();
            RefreshApplicableRules();
        };
        _environmentInput.SelectionChanged += (_, _) => RefreshApplicableRules();
        Content = BuildContent();
        LoadRules();
    }

    private Control BuildContent()
    {
        var generateButton = new Button { Content = "Generate contract", Padding = new Thickness(22, 10) };
        generateButton.Click += (_, _) => GenerateContract();
        var selectAllButton = new Button { Content = "Select all", Padding = new Thickness(14, 8) };
        selectAllButton.Click += (_, _) => SetRulesSelected(true);
        var clearAllButton = new Button { Content = "Clear all", Padding = new Thickness(14, 8) };
        clearAllButton.Click += (_, _) => SetRulesSelected(false);
        var copyButton = new Button { Content = "Copy", Padding = new Thickness(18, 10) };
        copyButton.Click += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_output.Text) && Clipboard is not null)
            {
                try
                {
                    await Clipboard.SetTextAsync(_output.Text);
                    _status.Text = "Contract copied to clipboard.";
                }
                catch (Exception exception) when (exception is InvalidOperationException or IOException)
                {
                    _status.Text = $"Unable to copy contract: {exception.Message}";
                }
            }
        };
        var saveButton = new Button { Content = "Save as...", Padding = new Thickness(18, 10) };
        saveButton.Click += async (_, _) => await SaveContract();

        var header = new StackPanel { Spacing = 6 };
        header.Children.Add(new TextBlock { Text = "ENOC Engineering Contract", FontSize = 30, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#152238")) });
        header.Children.Add(new TextBlock { Text = "Choose your development context and the rules the AI must enforce.", FontSize = 15, Foreground = Brushes.Gray });

        var form = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        form.Children.Add(LabeledControl("Task or feature request", _taskInput, 0, 0, 4));
        form.Children.Add(LabeledControl("Platform", _platformInput, 0, 1));
        form.Children.Add(LabeledControl("Development language", _languageInput, 1, 1));
        form.Children.Add(LabeledControl("Environment", _environmentInput, 2, 1, 1, "Select the deployment target. Dev is for local development, QA is for shared testing, and Production is the release configuration. This changes environment-specific requirements such as debug protection, logging, signing, and distribution."));
        form.Children.Add(LabeledControl("Output format", _formatInput, 3, 1));

        var ruleHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        ruleHeader.Children.Add(new TextBlock { Text = "Rules to apply", FontSize = 18, FontWeight = FontWeight.SemiBold });
        ruleHeader.Children.Add(selectAllButton);
        ruleHeader.Children.Add(clearAllButton);
        var rulePanel = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        rulePanel.Children.Add(ruleHeader);
        var rulesScroll = new ScrollViewer { Content = _rulesList, MaxHeight = 190, Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetRow(rulesScroll, 1);
        rulePanel.Children.Add(rulesScroll);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 6, 0, 0) };
        actions.Children.Add(generateButton);
        actions.Children.Add(copyButton);
        actions.Children.Add(saveButton);
        actions.Children.Add(_status);
        _status.VerticalAlignment = VerticalAlignment.Center;

        var outputPanel = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        outputPanel.Children.Add(new TextBlock { Text = "Generated contract", FontSize = 18, FontWeight = FontWeight.SemiBold });
        Grid.SetRow(_output, 1);
        outputPanel.Children.Add(_output);

        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), Margin = new Thickness(34) };
        layout.Children.Add(header);
        Grid.SetRow(form, 1);
        layout.Children.Add(form);
        Grid.SetRow(rulePanel, 2);
        layout.Children.Add(rulePanel);
        Grid.SetRow(actions, 3);
        layout.Children.Add(actions);
        Grid.SetRow(outputPanel, 4);
        layout.Children.Add(outputPanel);
        return layout;
    }

    private void LoadRules()
    {
        try
        {
            _catalog = _catalogService.Load(Path.Combine(AppContext.BaseDirectory, "rules"));
            RefreshApplicableRules();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or DirectoryNotFoundException)
        {
            _status.Text = $"Unable to load rules: {exception.Message}";
        }
    }

    private void GenerateContract()
    {
        if (string.IsNullOrWhiteSpace(_taskInput.Text))
        {
            _status.Text = "Enter a task before generating a contract.";
            return;
        }

        var selectedRules = _rulesList.Children.OfType<CheckBox>()
            .Where(checkBox => checkBox.IsChecked == true)
            .Select(checkBox => (EngineeringRule)checkBox.Tag!)
            .ToArray();
        if (selectedRules.Length == 0)
        {
            _status.Text = "Select at least one rule.";
            return;
        }

        var context = new ContractContext(
            _taskInput.Text.Trim(),
            _platformInput.SelectedItem?.ToString() ?? "android",
            _languageInput.Text ?? "Kotlin",
            _environmentInput.SelectedItem?.ToString() ?? "dev",
            "mobile");
        var contract = _contractService.Generate(context, selectedRules, _catalog?.SchemaVersion ?? "unknown");
        _output.Text = _contractService.Render(contract, _formatInput.SelectedItem?.ToString() ?? "markdown");
        _status.Text = $"{selectedRules.Length} rules included.";
    }

    private async Task SaveContract()
    {
        if (string.IsNullOrWhiteSpace(_output.Text))
        {
            _status.Text = "Generate a contract before saving.";
            return;
        }

        var extension = _formatInput.SelectedItem?.ToString() == "json" ? "json" : "md";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save engineering contract",
            SuggestedFileName = $"engineering-contract.{extension}",
            FileTypeChoices = new[] { new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = new[] { $"*.{extension}" } } }
        });
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(_output.Text);
            _status.Text = "Contract saved.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _status.Text = $"Unable to save contract: {exception.Message}";
        }
    }

    private void SetRulesSelected(bool selected)
    {
        foreach (var checkBox in _rulesList.Children.OfType<CheckBox>())
            checkBox.IsChecked = selected;
    }

    private void RefreshApplicableRules()
    {
        if (_catalog is null)
            return;

        var context = new ContractContext(
            _taskInput.Text?.Trim() ?? string.Empty,
            _platformInput.SelectedItem?.ToString() ?? "android",
            _languageInput.Text ?? "Kotlin",
            _environmentInput.SelectedItem?.ToString() ?? "dev",
            "mobile");
        var applicableRules = _catalog.ApplicableTo(context);
        _rulesList.Children.Clear();
        foreach (var rule in applicableRules)
        {
            _rulesList.Children.Add(new CheckBox
            {
                Content = $"{rule.Id} - {rule.Name} ({rule.Severity})",
                Tag = rule,
                IsChecked = true,
                Margin = new Thickness(4, 2)
            });
        }
        _status.Text = $"{applicableRules.Count} applicable rules loaded.";
    }

    private void UpdateLanguage()
    {
        _languageInput.Text = _platformInput.SelectedItem?.ToString() switch
        {
            "ios" => "Swift",
            "multiplatform" => "Flutter (Dart)",
            _ => "Kotlin"
        };
    }

    private static Control LabeledControl(string label, Control control, int column, int row, int columnSpan = 1, string? description = null)
    {
        var panel = new StackPanel { Spacing = 5 };
        var labelBlock = new TextBlock { Text = label, FontWeight = FontWeight.SemiBold };
        if (description is not null) ToolTip.SetTip(labelBlock, description);
        panel.Children.Add(labelBlock);
        panel.Children.Add(control);
        Grid.SetColumn(panel, column);
        Grid.SetRow(panel, row);
        Grid.SetColumnSpan(panel, columnSpan);
        return panel;
    }

    private static ComboBox CreateComboBox(params string[] values) => new() { ItemsSource = values, SelectedIndex = 0, MinHeight = 42 };
}
