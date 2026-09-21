using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AI_Enoc_Engineering_Agent;

public sealed class MainWindow : Window
{
    private readonly IRuleCatalogService _catalogService;
    private readonly IContractService _contractService;
    private readonly IUserSettingsService _settingsService;
    private readonly TextBox _taskInput = new() { Watermark = "Example: Create a secure employee login feature", MinHeight = 42 };
    private readonly ComboBox _platformInput = CreateComboBox("android", "ios", "multiplatform");
    private readonly TextBox _languageInput = new() { IsReadOnly = true, Text = "Kotlin", MinHeight = 42 };
    private readonly ComboBox _environmentInput = CreateComboBox("dev", "qa", "production");
    private readonly ComboBox _formatInput = CreateComboBox("markdown", "json");
    private readonly TextBox _ruleSearch = new() { Watermark = "Search rules by ID, name, category, or severity...", MinHeight = 38 };
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
    private readonly HashSet<string> _selectedRuleIds = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<EngineeringRule> _applicableRules = Array.Empty<EngineeringRule>();

    public MainWindow(
        IRuleCatalogService? catalogService = null,
        IContractService? contractService = null,
        IUserSettingsService? settingsService = null)
    {
        _catalogService = catalogService ?? new RuleCatalogService();
        _contractService = contractService ?? new ContractService();
        _settingsService = settingsService ?? new UserSettingsService();
        Title = "ENOC Engineering Guardrails";
        Width = 1160;
        Height = 820;
        MinWidth = 820;
        MinHeight = 620;
        Background = new SolidColorBrush(Color.Parse("#F4F7F8"));
        _platformInput.SelectionChanged += (_, _) =>
        {
            UpdateLanguage();
            RefreshApplicableRules();
            SaveSettings();
        };
        _environmentInput.SelectionChanged += (_, _) =>
        {
            RefreshApplicableRules();
            SaveSettings();
        };
        _formatInput.SelectionChanged += (_, _) => SaveSettings();
        _ruleSearch.TextChanged += (_, _) => RefreshRuleList();
        Content = BuildContent();
        RestoreSettings();
        LoadRules();
    }

    private Control BuildContent()
    {
        var generateButton = new Button { Content = "Generate guardrails", Padding = new Thickness(22, 10), Classes = { "primary" } };
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

        var headerCopy = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        headerCopy.Children.Add(new TextBlock { Text = "ENOC Engineering Guardrails", FontSize = 28, FontWeight = FontWeight.Bold, Foreground = Brushes.White });
        headerCopy.Children.Add(new TextBlock { Text = "Create consistent, reviewable engineering instructions for your AI agent.", FontSize = 14, Foreground = new SolidColorBrush(Color.Parse("#D7E8E8")) });
        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#123C46")),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22),
            Child = headerCopy
        };

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
        var ruleControls = new StackPanel { Spacing = 8 };
        ruleControls.Children.Add(ruleHeader);
        ruleControls.Children.Add(_ruleSearch);
        rulePanel.Children.Add(ruleControls);
        var rulesScroll = new ScrollViewer { Content = _rulesList, MaxHeight = 190, Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetRow(rulesScroll, 1);
        rulePanel.Children.Add(rulesScroll);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 6, 0, 0) };
        actions.Children.Add(generateButton);
        actions.Children.Add(copyButton);
        actions.Children.Add(saveButton);
        actions.Children.Add(_status);
        _status.VerticalAlignment = VerticalAlignment.Center;

        var outputPanel = new StackPanel { Spacing = 10 };
        outputPanel.Children.Add(new TextBlock { Text = "Generated guardrails", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse("#123C46")) });
        _output.Height = 360;
        outputPanel.Children.Add(_output);

        var pageContent = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(30),
            Children =
            {
                header,
                Card(form),
                Card(rulePanel),
                actions,
                Card(outputPanel)
            }
        };
        var page = new ScrollViewer
        {
            Content = pageContent,
            VerticalContentAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        page.AttachedToVisualTree += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                page.Offset = new Vector(0, 0);
                _taskInput.Focus();
            }, DispatcherPriority.Loaded);
        };
        return page;
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

        var selectedRules = _applicableRules.Where(rule => _selectedRuleIds.Contains(rule.Id)).ToArray();
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
        SaveSettings();
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
            Title = "Save engineering guardrails",
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
        foreach (var rule in _applicableRules)
        {
            if (selected)
                _selectedRuleIds.Add(rule.Id);
            else
                _selectedRuleIds.Remove(rule.Id);
        }
        RefreshRuleList();
        SaveSettings();
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
        _applicableRules = applicableRules;
        var selectedForContext = _selectedRuleIds.Count == 0
            ? applicableRules.Select(rule => rule.Id)
            : _selectedRuleIds.Intersect(applicableRules.Select(rule => rule.Id), StringComparer.OrdinalIgnoreCase);
        _selectedRuleIds.Clear();
        foreach (var ruleId in selectedForContext)
            _selectedRuleIds.Add(ruleId);
        RefreshRuleList();
        _status.Text = $"{applicableRules.Count} applicable rules loaded; {_selectedRuleIds.Count} selected.";
    }

    private void RefreshRuleList()
    {
        if (_catalog is null)
            return;

        var filter = _ruleSearch.Text?.Trim() ?? string.Empty;
        _rulesList.Children.Clear();
        var visibleRules = _applicableRules
            .Where(rule => string.IsNullOrEmpty(filter) ||
                $"{rule.Id} {rule.Name} {rule.Category} {rule.Severity}".Contains(filter, StringComparison.OrdinalIgnoreCase))
            .GroupBy(rule => rule.Category, StringComparer.OrdinalIgnoreCase);
        foreach (var category in visibleRules)
        {
            _rulesList.Children.Add(new TextBlock
            {
                Text = category.Key.ToUpperInvariant(),
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.Gray,
                Margin = new Thickness(4, 8, 4, 2)
            });
            foreach (var rule in category)
            {
                var checkBox = new CheckBox
                {
                    Content = $"{rule.Id} - {rule.Name} ({rule.Severity})",
                    Tag = rule,
                    IsChecked = _selectedRuleIds.Contains(rule.Id),
                    Margin = new Thickness(4, 2)
                };
                checkBox.IsCheckedChanged += (_, _) =>
                {
                    if (checkBox.IsChecked == true)
                        _selectedRuleIds.Add(rule.Id);
                    else
                        _selectedRuleIds.Remove(rule.Id);
                    SaveSettings();
                };
                _rulesList.Children.Add(checkBox);
            }
        }
    }

    private void RestoreSettings()
    {
        var settings = _settingsService.Load();
        _taskInput.Text = settings.Task;
        SelectValue(_platformInput, settings.Platform);
        SelectValue(_environmentInput, settings.Environment);
        SelectValue(_formatInput, settings.Format);
        foreach (var ruleId in settings.SelectedRuleIds)
            _selectedRuleIds.Add(ruleId);
        UpdateLanguage();
    }

    private void SaveSettings()
    {
        try
        {
            _settingsService.Save(new UserSettings(
                _taskInput.Text?.Trim() ?? string.Empty,
                _platformInput.SelectedItem?.ToString() ?? "android",
                _environmentInput.SelectedItem?.ToString() ?? "dev",
                _formatInput.SelectedItem?.ToString() ?? "markdown",
                _selectedRuleIds.OrderBy(ruleId => ruleId).ToArray()));
        }
        catch (IOException)
        {
            _status.Text = "Settings could not be saved; your contract is still available.";
        }
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
        var panel = new StackPanel { Spacing = 7 };
        var labelBlock = new TextBlock { Text = label, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse("#123C46")) };
        if (description is not null) ToolTip.SetTip(labelBlock, description);
        panel.Children.Add(labelBlock);
        panel.Children.Add(control);
        Grid.SetColumn(panel, column);
        Grid.SetRow(panel, row);
        Grid.SetColumnSpan(panel, columnSpan);
        panel.Margin = new Thickness(column == 3 ? 0 : 0, row == 1 ? 10 : 0, column == 3 ? 0 : 10, 0);
        return panel;
    }

    private static ComboBox CreateComboBox(params string[] values) => new() { ItemsSource = values, SelectedIndex = 0, MinHeight = 42 };

    private static Border Card(Control content) => new()
    {
        Background = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.Parse("#DCE7E8")),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(20),
        Child = content
    };

    private static void SelectValue(ComboBox comboBox, string value)
    {
        var index = comboBox.ItemsSource
            ?.Cast<string>()
            .ToList()
            .FindIndex(item => item.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? -1;
        if (index >= 0)
            comboBox.SelectedIndex = index;
    }
}
