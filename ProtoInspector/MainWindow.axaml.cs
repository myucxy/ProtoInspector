using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProtoInspector.Models;
using ProtoInspector.Services;

namespace ProtoInspector;

public partial class MainWindow : Window
{
    private static readonly IBrush BusyBrush = new SolidColorBrush(Color.Parse("#B45309"));
    private static readonly IBrush IdleBrush = new SolidColorBrush(Color.Parse("#475569"));
    private static readonly IBrush LoadedBrush = new SolidColorBrush(Color.Parse("#15803D"));

    private readonly ObservableCollection<ProtocolFileOption> _protocolFiles = [];
    private readonly ObservableCollection<ProtocolMessageDefinition> _filteredMessages = [];
    private IReadOnlyList<ProtocolMessageDefinition> _allMessages = Array.Empty<ProtocolMessageDefinition>();
    private IReadOnlyList<ProtocolGenerationItem> _missingItems = Array.Empty<ProtocolGenerationItem>();

    private ComboBox _protocolFileComboBox = null!;
    private AutoCompleteBox _messageTypeAutoCompleteBox = null!;
    private TextBlock _inputTitleTextBlock = null!;
    private TextBox _byteInputTextBox = null!;
    private TextBox _resultTextBox = null!;
    private TextBlock _statusTextBlock = null!;
    private TextBlock _workspacePathTextBlock = null!;
    private TextBlock _selectedProtocolTextBlock = null!;
    private TextBlock _sessionStateTextBlock = null!;
    private TextBlock _messageCountTextBlock = null!;
    private TextBlock _missingFilesSummaryTextBlock = null!;
    private TextBox _missingFilesTextBox = null!;
    private Button _refreshFilesButton = null!;
    private Button _checkProtocolsButton = null!;
    private Button _loadProtocolButton = null!;
    private Button _unloadProtocolButton = null!;
    private Button _parseButton = null!;
    private Button _serializeButton = null!;
    private Button _generateSampleButton = null!;
    private Button _loadByteFileButton = null!;
    private Button _generateMissingFilesButton = null!;
    private bool _updatingMessageTypeText;

    private ProtocolSession? _currentSession;
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();
        FindControls();
        WireEvents();
        InitializeDefaults();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void FindControls()
    {
        _protocolFileComboBox = Required<ComboBox>(nameof(ProtocolFileComboBox));
        _messageTypeAutoCompleteBox = Required<AutoCompleteBox>(nameof(MessageTypeAutoCompleteBox));
        _inputTitleTextBlock = Required<TextBlock>(nameof(InputTitleTextBlock));
        _byteInputTextBox = Required<TextBox>(nameof(ByteInputTextBox));
        _resultTextBox = Required<TextBox>(nameof(ResultTextBox));
        _statusTextBlock = Required<TextBlock>(nameof(StatusTextBlock));
        _workspacePathTextBlock = Required<TextBlock>(nameof(WorkspacePathTextBlock));
        _selectedProtocolTextBlock = Required<TextBlock>(nameof(SelectedProtocolTextBlock));
        _sessionStateTextBlock = Required<TextBlock>(nameof(SessionStateTextBlock));
        _messageCountTextBlock = Required<TextBlock>(nameof(MessageCountTextBlock));
        _missingFilesSummaryTextBlock = Required<TextBlock>(nameof(MissingFilesSummaryTextBlock));
        _missingFilesTextBox = Required<TextBox>(nameof(MissingFilesTextBox));
        _refreshFilesButton = Required<Button>(nameof(RefreshFilesButton));
        _checkProtocolsButton = Required<Button>(nameof(CheckProtocolsButton));
        _loadProtocolButton = Required<Button>(nameof(LoadProtocolButton));
        _unloadProtocolButton = Required<Button>(nameof(UnloadProtocolButton));
        _parseButton = Required<Button>(nameof(ParseButton));
        _serializeButton = Required<Button>(nameof(SerializeButton));
        _generateSampleButton = Required<Button>(nameof(GenerateSampleButton));
        _loadByteFileButton = Required<Button>(nameof(LoadByteFileButton));
        _generateMissingFilesButton = Required<Button>(nameof(GenerateMissingFilesButton));

        _protocolFileComboBox.ItemsSource = _protocolFiles;
        _messageTypeAutoCompleteBox.ItemsSource = _filteredMessages;
        _messageTypeAutoCompleteBox.ItemFilter = (search, item) =>
        {
            if (item is not ProtocolMessageDefinition definition)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(search)
                   || definition.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                   || definition.FullName.Contains(search, StringComparison.OrdinalIgnoreCase);
        };
        _messageTypeAutoCompleteBox.ItemSelector = (_, item) =>
            item is ProtocolMessageDefinition definition ? definition.DisplayName : string.Empty;
        _workspacePathTextBlock.Text = $"工作区: {WorkspacePaths.GetWorkspaceRoot()}";
    }

    private T Required<T>(string name) where T : Control
    {
        return this.FindControl<T>(name) ?? throw new InvalidOperationException($"未找到控件: {name}");
    }

    private void WireEvents()
    {
        _refreshFilesButton.Click += (_, _) => RefreshProtocolFiles();
        _checkProtocolsButton.Click += (_, _) => CheckProtocolsOnDemand();
        _loadProtocolButton.Click += async (_, _) => await LoadSelectedProtocolAsync();
        _unloadProtocolButton.Click += (_, _) => UnloadCurrentProtocol();
        _parseButton.Click += (_, _) => ParseCurrentMessage();
        _serializeButton.Click += (_, _) => SerializeCurrentMessage();
        _generateSampleButton.Click += async (_, _) => await GenerateSampleAsync();
        _loadByteFileButton.Click += async (_, _) => await LoadByteFileIntoInputAsync();
        _generateMissingFilesButton.Click += async (_, _) => await GenerateMissingFilesAsync();
        _protocolFileComboBox.SelectionChanged += (_, _) => UpdateOverview();
        _messageTypeAutoCompleteBox.SelectionChanged += (_, _) => UpdateOverview();
        _messageTypeAutoCompleteBox.PropertyChanged += (_, args) =>
        {
            if (!_updatingMessageTypeText && args.Property == AutoCompleteBox.TextProperty)
            {
                UpdateOverview();
            }
        };
        Closing += (_, _) => _currentSession?.Dispose();
    }

    private void InitializeDefaults()
    {
        LoadDefaultByteText();
        RefreshMissingGeneratedFiles();
        RefreshProtocolFiles();
        SetStatus("就绪。请选择协议文件并加载。");
    }

    private void LoadDefaultByteText()
    {
        var defaultByteFile = WorkspacePaths.GetDefaultByteFile();
        if (File.Exists(defaultByteFile))
        {
            _byteInputTextBox.Text = File.ReadAllText(defaultByteFile, Encoding.UTF8);
        }
    }

    private void RefreshProtocolFiles()
    {
        var directory = WorkspacePaths.GetGeneratedDirectory();
        var selectedFilePath = _currentSession?.SourceFile ?? (_protocolFileComboBox.SelectedItem as ProtocolFileOption)?.FullPath;
        _protocolFiles.Clear();

        if (!Directory.Exists(directory))
        {
            _protocolFileComboBox.SelectedItem = null;
            SetStatus($"协议目录不存在: {directory}");
            UpdateOverview();
            return;
        }

        foreach (var file in Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                     .Select(path => new ProtocolFileOption
                     {
                         DisplayName = Path.GetFileName(path),
                         FullPath = path
                     }))
        {
            _protocolFiles.Add(file);
        }

        _protocolFileComboBox.SelectedItem = _protocolFiles.FirstOrDefault(file =>
                                             string.Equals(file.FullPath, selectedFilePath, StringComparison.OrdinalIgnoreCase))
                                         ?? _protocolFiles.FirstOrDefault();
        SetStatus(_protocolFiles.Count > 0
            ? $"发现 {_protocolFiles.Count} 个协议文件。"
            : "当前目录没有找到 .cs 协议文件。");
        UpdateOverview();
    }

    private async Task LoadSelectedProtocolAsync()
    {
        if (_isBusy)
        {
            return;
        }

        if (_protocolFileComboBox.SelectedItem is not ProtocolFileOption protocolFile)
        {
            SetStatus("请先选择协议文件。");
            UpdateOverview();
            return;
        }

        try
        {
            ToggleBusy(true, "正在编译并加载协议...");
            UnloadCurrentProtocol(setStatus: false);

            var session = await Task.Run(() => ProtocolCompiler.Compile(protocolFile.FullPath));
            _currentSession = session;
            _allMessages = session.Messages;

            SetMessageTypeText(string.Empty);
            ResetMessageTypeList();
            _resultTextBox.Text = string.Empty;
            SetStatus($"已加载协议: {protocolFile.DisplayName}，消息类型 {session.Messages.Count} 个。");
        }
        catch (Exception ex)
        {
            _resultTextBox.Text = ex.Message;
            SetStatus("协议加载失败。");
        }
        finally
        {
            ToggleBusy(false, null);
        }
    }

    private void UnloadCurrentProtocol(bool setStatus = true)
    {
        _allMessages = Array.Empty<ProtocolMessageDefinition>();
        _filteredMessages.Clear();
        _messageTypeAutoCompleteBox.SelectedItem = null;
        SetMessageTypeText(string.Empty);
        _resultTextBox.Text = string.Empty;

        if (_currentSession is null)
        {
            UpdateOverview();
            return;
        }

        _currentSession.Dispose();
        _currentSession = null;
        if (setStatus)
        {
            SetStatus("已清空当前协议。");
        }

        UpdateOverview();
    }

    private void ParseCurrentMessage()
    {
        if (_currentSession is null)
        {
            SetStatus("请先加载协议。");
            return;
        }

        if (_messageTypeAutoCompleteBox.SelectedItem is not ProtocolMessageDefinition definition)
        {
            SetStatus("请先选择消息类型。");
            return;
        }

        try
        {
            var inputText = _byteInputTextBox.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(inputText))
            {
                _resultTextBox.Text = "请输入要反序列化的字节码，支持十六进制、十进制或 Base64。";
                SetStatus("反序列化失败：输入为空。");
                return;
            }

            var bytes = ByteInputParser.Parse(inputText);
            var message = definition.Parser.ParseFrom(bytes);
            _resultTextBox.Text = ProtoMessageFormatter.Format(message, bytes, definition.DisplayName);
            SetStatus($"解析成功。字节长度: {bytes.Length}");
        }
        catch (Exception ex)
        {
            _resultTextBox.Text = ex.ToString();
            SetStatus("解析失败。");
        }
    }

    private void SerializeCurrentMessage()
    {
        if (_currentSession is null)
        {
            SetStatus("请先加载协议。");
            return;
        }

        if (_messageTypeAutoCompleteBox.SelectedItem is not ProtocolMessageDefinition definition)
        {
            SetStatus("请先选择消息类型。");
            return;
        }

        try
        {
            var inputText = _byteInputTextBox.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(inputText))
            {
                _resultTextBox.Text = "请输入要序列化的 Protobuf JSON 对象。";
                SetStatus("序列化失败：输入为空。");
                return;
            }

            var result = ProtoMessageSerializer.SerializeFromJson(inputText, definition);
            _resultTextBox.Text = ProtoMessageFormatter.FormatSerialized(result.Message, result.Bytes, definition.DisplayName);
            SetStatus($"序列化成功。字节长度: {result.Bytes.Length}");
        }
        catch (Exception ex)
        {
            _resultTextBox.Text = ex.ToString();
            SetStatus("序列化失败。");
        }
    }

    private async Task GenerateSampleAsync()
    {
        if (_currentSession is null)
        {
            SetStatus("请先加载协议。");
            return;
        }

        if (_messageTypeAutoCompleteBox.SelectedItem is not ProtocolMessageDefinition definition)
        {
            SetStatus("请先选择消息类型。");
            return;
        }

        try
        {
            var sample = ProtoJsonSampleGenerator.Generate(definition);
            var action = await ShowSampleActionDialogAsync();
            if (action == JsonSampleAction.Cancel)
            {
                return;
            }

            if (action == JsonSampleAction.Edit)
            {
                var edited = await new JsonSampleEditWindow(sample).ShowDialog<string?>(this);
                if (edited is null)
                {
                    return;
                }

                sample = edited;
            }

            _byteInputTextBox.Text = sample;
            SetStatus("已生成 JSON 样例。");
        }
        catch (Exception ex)
        {
            _resultTextBox.Text = ex.ToString();
            SetStatus("生成样例失败。");
        }
    }

    private async Task<JsonSampleAction> ShowSampleActionDialogAsync()
    {
        var dialog = new Window
        {
            Title = "生成 JSON 样例",
            Width = 360,
            Height = 180,
            MinWidth = 360,
            MinHeight = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White
        };
        dialog.Content = CreateSampleActionContent(action => dialog.Close(action));

        return await dialog.ShowDialog<JsonSampleAction>(this);
    }

    private static Control CreateSampleActionContent(Action<JsonSampleAction> close)
    {
        var dialog = new Grid
        {
            Margin = new Thickness(14),
            RowDefinitions = new RowDefinitions("*,Auto")
        };

        dialog.Children.Add(new TextBlock
        {
            Text = "已根据当前消息类型生成 JSON 样例，是否需要先修改内容？",
            TextWrapping = TextWrapping.Wrap,
            Foreground = IdleBrush,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(buttons, 1);

        buttons.Children.Add(CreateDialogButton("取消", JsonSampleAction.Cancel, close));
        buttons.Children.Add(CreateDialogButton("不修改", JsonSampleAction.UseDirectly, close));
        buttons.Children.Add(CreateDialogButton("修改", JsonSampleAction.Edit, close));
        dialog.Children.Add(buttons);

        return dialog;
    }

    private static Button CreateDialogButton(string text, JsonSampleAction action, Action<JsonSampleAction> close)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 76,
            Height = 30,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        button.Click += (_, _) => close(action);
        return button;
    }

    private async Task LoadByteFileIntoInputAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            return;
        }

        var startFolder = await TryGetStartFolderAsync(topLevel.StorageProvider);
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择输入文件",
            SuggestedStartLocation = startFolder,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text Files") { Patterns = ["*.txt"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] }
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        _byteInputTextBox.Text = await reader.ReadToEndAsync();
        SetStatus($"已读取输入文件: {file.Name}");
    }

    private async Task<IStorageFolder?> TryGetStartFolderAsync(IStorageProvider storageProvider)
    {
        var startFolder = await storageProvider.TryGetFolderFromPathAsync(WorkspacePaths.GetBytesDirectory());
        if (startFolder is not null)
        {
            return startFolder;
        }

        var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return await storageProvider.TryGetFolderFromPathAsync(fallback);
    }

    private void ToggleBusy(bool busy, string? message)
    {
        _isBusy = busy;
        _refreshFilesButton.IsEnabled = !busy;
        _checkProtocolsButton.IsEnabled = !busy;
        _loadProtocolButton.IsEnabled = !busy;
        _unloadProtocolButton.IsEnabled = !busy;
        _parseButton.IsEnabled = !busy;
        _serializeButton.IsEnabled = !busy;
        _generateSampleButton.IsEnabled = !busy;
        _loadByteFileButton.IsEnabled = !busy;
        _generateMissingFilesButton.IsEnabled = !busy && _missingItems.Count > 0;
        _protocolFileComboBox.IsEnabled = !busy;
        _messageTypeAutoCompleteBox.IsEnabled = !busy;

        if (!string.IsNullOrWhiteSpace(message))
        {
            SetStatus(message);
        }

        UpdateOverview();
    }

    private void SetStatus(string message)
    {
        if (_statusTextBlock.Text != message)
        {
            _statusTextBlock.Text = message;
        }
    }

    private void UpdateOverview()
    {
        var selectedFile = _currentSession is not null
            ? Path.GetFileName(_currentSession.SourceFile)
            : (_protocolFileComboBox.SelectedItem as ProtocolFileOption)?.DisplayName ?? "未选择";
        _selectedProtocolTextBlock.Text = selectedFile;
        _sessionStateTextBlock.Text = _isBusy ? "正在处理..." : _currentSession is null ? "未加载" : "已加载";
        _sessionStateTextBlock.Foreground = _isBusy ? BusyBrush : _currentSession is null ? IdleBrush : LoadedBrush;

        var totalCount = _currentSession?.Messages.Count ?? 0;
        _messageCountTextBlock.Text = totalCount == 0 ? "0" : $"{_filteredMessages.Count} / {totalCount}";
    }

    private void CheckProtocolsOnDemand()
    {
        RefreshMissingGeneratedFiles();
        RefreshProtocolFiles();
        SetStatus(_missingItems.Count == 0 ? "当前没有发现缺失的协议文件。" : $"发现 {_missingItems.Count} 个待生成协议文件。");
    }

    private void RefreshMissingGeneratedFiles()
    {
        try
        {
            _missingItems = ProtocolWorkspaceSynchronizer.FindMissingGeneratedFiles();
            _missingFilesSummaryTextBlock.Text = _missingItems.Count == 0
                ? "当前没有发现缺失的 generated 协议文件。"
                : $"检测到 {_missingItems.Count} 个 proto 文件还没有生成对应的 C# 文件。";
            _missingFilesTextBox.Text = _missingItems.Count == 0
                ? "状态正常。"
                : string.Join(Environment.NewLine, _missingItems.Select(item =>
                    $"{Path.GetFileName(item.ProtoFile)} -> {Path.GetFileName(item.GeneratedFile)}"));
        }
        catch (Exception ex)
        {
            _missingItems = Array.Empty<ProtocolGenerationItem>();
            _missingFilesSummaryTextBlock.Text = "检测协议状态失败。";
            _missingFilesTextBox.Text = ex.Message;
        }

        _generateMissingFilesButton.IsEnabled = !_isBusy && _missingItems.Count > 0;
    }

    private async Task GenerateMissingFilesAsync()
    {
        if (_isBusy || _missingItems.Count == 0)
        {
            return;
        }

        try
        {
            ToggleBusy(true, "正在生成缺失的协议文件...");
            await Task.Run(() => ProtocolWorkspaceSynchronizer.GenerateMissingFiles(_missingItems));
            RefreshMissingGeneratedFiles();
            RefreshProtocolFiles();
            SetStatus("缺失的协议文件已生成。");
        }
        catch (Exception ex)
        {
            _missingFilesTextBox.Text = ex.Message;
            SetStatus("自动生成协议失败。");
        }
        finally
        {
            ToggleBusy(false, null);
        }
    }

    private void ResetMessageTypeList()
    {
        _filteredMessages.Clear();
        foreach (var item in _allMessages)
        {
            _filteredMessages.Add(item);
        }

        _messageTypeAutoCompleteBox.SelectedItem = null;
        _messageTypeAutoCompleteBox.IsDropDownOpen = _filteredMessages.Count > 0;
        UpdateOverview();
    }

    private void SetMessageTypeText(string text)
    {
        if (string.Equals(_messageTypeAutoCompleteBox.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        _updatingMessageTypeText = true;
        _messageTypeAutoCompleteBox.Text = text;
        _updatingMessageTypeText = false;
    }
}

internal enum JsonSampleAction
{
    Cancel,
    UseDirectly,
    Edit
}
