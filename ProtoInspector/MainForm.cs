using System.Text;
using ProtoInspector.Models;
using ProtoInspector.Services;

namespace ProtoInspector;

public sealed class MainForm : Form
{
    private readonly ComboBox _protocolFileComboBox = new();
    private readonly TextBox _messageTypeSearchTextBox = new();
    private readonly ComboBox _messageTypeComboBox = new();
    private readonly TextBox _byteInputTextBox = new();
    private readonly RichTextBox _resultTextBox = new();
    private readonly SplitContainer _editorSplit = new();
    private readonly Label _statusLabel = new();
    private readonly Label _workspacePathLabel = new();
    private readonly Label _selectedProtocolValueLabel = new();
    private readonly Label _sessionStateValueLabel = new();
    private readonly Label _messageCountValueLabel = new();
    private readonly Button _refreshFilesButton = new();
    private readonly Button _checkProtocolsButton = new();
    private readonly Button _loadProtocolButton = new();
    private readonly Button _unloadProtocolButton = new();
    private readonly Button _parseButton = new();
    private readonly Button _loadByteFileButton = new();

    private ProtocolSession? _currentSession;
    private IReadOnlyList<ProtocolMessageDefinition> _filteredMessages = Array.Empty<ProtocolMessageDefinition>();
    private bool _isBusy;

    public MainForm()
    {
        Text = "Proto Inspector";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1240, 760);
        Width = 1460;
        Height = 920;
        BackColor = Color.FromArgb(243, 246, 250);
        DoubleBuffered = true;

        BuildLayout();
        WireEvents();
        InitializeDefaults();
        Shown += (_, _) => AdjustEditorSplit();
        Resize += (_, _) => AdjustEditorSplit();
    }

    private void BuildLayout()
    {
        SuspendLayout();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headerCard = CreateCard("Proto Inspector", "动态加载单个协议，选择消息类型后解析字节内容", out var headerBody);
        _workspacePathLabel.Dock = DockStyle.Fill;
        _workspacePathLabel.AutoEllipsis = true;
        _workspacePathLabel.Margin = new Padding(0);
        _workspacePathLabel.ForeColor = Color.FromArgb(71, 85, 105);
        _workspacePathLabel.Text = $"工作区: {WorkspacePaths.GetWorkspaceRoot()}";
        headerBody.Controls.Add(_workspacePathLabel, 0, 0);

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 16, 0, 16),
            BackColor = Color.Transparent
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sidebarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0, 0, 16, 0),
            AutoScroll = true,
            BackColor = Color.Transparent
        };
        sidebarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        ConfigureSummaryValueLabel(_selectedProtocolValueLabel);
        ConfigureSummaryValueLabel(_sessionStateValueLabel);
        ConfigureSummaryValueLabel(_messageCountValueLabel);

        var overviewCard = CreateCard("当前概览", "常驻显示当前协议加载与筛选状态", out var overviewBody);
        overviewBody.Controls.Add(CreateInfoRow("协议文件", _selectedProtocolValueLabel), 0, 0);
        overviewBody.Controls.Add(CreateInfoRow("加载状态", _sessionStateValueLabel), 0, 1);
        overviewBody.Controls.Add(CreateInfoRow("消息类型", _messageCountValueLabel, false), 0, 2);

        _protocolFileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _protocolFileComboBox.Dock = DockStyle.Fill;
        _messageTypeSearchTextBox.Dock = DockStyle.Fill;
        _messageTypeSearchTextBox.PlaceholderText = "输入类名关键字";
        _messageTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _messageTypeComboBox.Dock = DockStyle.Fill;

        var protocolCard = CreateCard("协议与消息", "先选择协议，再用关键字快速定位消息类型", out var protocolBody);
        protocolBody.Controls.Add(CreateStackedField("协议文件", _protocolFileComboBox), 0, 0);
        protocolBody.Controls.Add(CreateStackedField("类型搜索", _messageTypeSearchTextBox), 0, 1);
        protocolBody.Controls.Add(CreateStackedField("消息类型", _messageTypeComboBox, false), 0, 2);

        var actionCard = CreateCard("常用操作", "主操作更突出，辅助操作保留在同一区域", out var actionBody);
        var actionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0)
        };
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        ConfigureButton(_refreshFilesButton, "刷新协议", fill: true);
        ConfigureButton(_checkProtocolsButton, "重新检测", fill: true);
        ConfigureButton(_loadProtocolButton, "加载协议", primary: true, fill: true);
        ConfigureButton(_unloadProtocolButton, "清空当前", fill: true);

        actionLayout.Controls.Add(_refreshFilesButton, 0, 0);
        actionLayout.Controls.Add(_checkProtocolsButton, 1, 0);
        actionLayout.Controls.Add(_loadProtocolButton, 0, 1);
        actionLayout.Controls.Add(_unloadProtocolButton, 1, 1);
        actionBody.Controls.Add(actionLayout, 0, 0);

        sidebarLayout.Controls.Add(overviewCard, 0, 0);
        sidebarLayout.Controls.Add(protocolCard, 0, 1);
        sidebarLayout.Controls.Add(actionCard, 0, 2);

        _editorSplit.Dock = DockStyle.Fill;
        _editorSplit.Orientation = Orientation.Vertical;
        _editorSplit.BorderStyle = BorderStyle.FixedSingle;
        _editorSplit.SplitterWidth = 8;
        _editorSplit.Panel1.Padding = new Padding(0, 0, 8, 0);
        _editorSplit.Panel2.Padding = new Padding(8, 0, 0, 0);

        var inputCard = CreateCard("输入字节码", "支持十六进制、十进制数组、0x、连续十六进制串", out var inputBody, fillBody: true);
        var inputLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var inputActionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        inputActionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputActionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        inputActionPanel.Controls.Add(new Label
        {
            Text = "可直接读取 byte.txt，也可粘贴任意待解析文本。",
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(100, 116, 139),
            Padding = new Padding(0, 10, 0, 0),
            Margin = new Padding(0)
        }, 0, 0);

        var inputButtonPanel = CreateButtonPanel();
        ConfigureButton(_loadByteFileButton, "读取 byte.txt");
        ConfigureButton(_parseButton, "开始解析", primary: true);
        inputButtonPanel.Controls.Add(_loadByteFileButton);
        inputButtonPanel.Controls.Add(_parseButton);
        inputActionPanel.Controls.Add(inputButtonPanel, 1, 0);

        _byteInputTextBox.Multiline = true;
        _byteInputTextBox.ScrollBars = ScrollBars.Vertical;
        _byteInputTextBox.AcceptsReturn = true;
        _byteInputTextBox.AcceptsTab = true;
        _byteInputTextBox.WordWrap = true;
        _byteInputTextBox.Font = new Font("Consolas", 10F);
        _byteInputTextBox.Dock = DockStyle.Fill;
        _byteInputTextBox.BorderStyle = BorderStyle.FixedSingle;
        _byteInputTextBox.BackColor = Color.FromArgb(249, 250, 251);

        inputLayout.Controls.Add(inputActionPanel, 0, 0);
        inputLayout.Controls.Add(_byteInputTextBox, 0, 1);
        inputBody.Controls.Add(inputLayout, 0, 0);

        var resultCard = CreateCard("解析结果", "根据当前消息类型格式化输出解析内容", out var resultBody, fillBody: true);
        _resultTextBox.Dock = DockStyle.Fill;
        _resultTextBox.ReadOnly = true;
        _resultTextBox.Font = new Font("Consolas", 10F);
        _resultTextBox.WordWrap = true;
        _resultTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        _resultTextBox.BorderStyle = BorderStyle.FixedSingle;
        _resultTextBox.BackColor = Color.FromArgb(249, 250, 251);
        resultBody.Controls.Add(_resultTextBox, 0, 0);

        _editorSplit.Panel1.Controls.Add(inputCard);
        _editorSplit.Panel2.Controls.Add(resultCard);

        contentLayout.Controls.Add(sidebarLayout, 0, 0);
        contentLayout.Controls.Add(_editorSplit, 1, 0);

        var statusPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 28,
            Padding = new Padding(4, 0, 4, 0),
            BackColor = Color.Transparent
        };
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.FromArgb(71, 85, 105);
        _statusLabel.Location = new Point(0, 6);
        statusPanel.Controls.Add(_statusLabel);

        root.Controls.Add(headerCard, 0, 0);
        root.Controls.Add(contentLayout, 0, 1);
        root.Controls.Add(statusPanel, 0, 2);

        Controls.Add(root);
        ResumeLayout();
    }

    private void WireEvents()
    {
        _refreshFilesButton.Click += (_, _) => RefreshProtocolFiles();
        _checkProtocolsButton.Click += (_, _) => CheckProtocolsOnDemand();
        _loadProtocolButton.Click += async (_, _) => await LoadSelectedProtocolAsync();
        _unloadProtocolButton.Click += (_, _) => UnloadCurrentProtocol();
        _parseButton.Click += (_, _) => ParseCurrentMessage();
        _loadByteFileButton.Click += (_, _) => LoadByteFileIntoInput();
        _messageTypeSearchTextBox.TextChanged += (_, _) => ApplyMessageTypeFilter();
        _protocolFileComboBox.SelectedIndexChanged += (_, _) => UpdateOverview();
    }

    private void InitializeDefaults()
    {
        EnsureGeneratedProtocols(showMessageWhenNothingMissing: false);
        LoadDefaultByteText();
        RefreshProtocolFiles();
        SetStatus("就绪。请选择协议文件并加载。");
        UpdateOverview();
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
        if (!Directory.Exists(directory))
        {
            _protocolFileComboBox.DataSource = null;
            SetStatus($"协议目录不存在: {directory}");
            UpdateOverview();
            return;
        }

        var files = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => new ProtocolFileOption
            {
                DisplayName = Path.GetFileName(path),
                FullPath = path
            })
            .ToArray();

        _protocolFileComboBox.DataSource = files;
        _protocolFileComboBox.DisplayMember = nameof(ProtocolFileOption.DisplayName);
        _protocolFileComboBox.SelectedIndex = files.Length > 0 ? 0 : -1;

        SetStatus(files.Length > 0
            ? $"发现 {files.Length} 个协议文件。"
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
            UnloadCurrentProtocol();

            var session = await Task.Run(() => ProtocolCompiler.Compile(protocolFile.FullPath));
            _currentSession = session;

            _messageTypeSearchTextBox.Clear();
            ApplyMessageTypeFilter();

            _resultTextBox.Clear();
            SetStatus($"已加载协议: {protocolFile.DisplayName}，消息类型 {session.Messages.Count} 个。");
            UpdateOverview();
        }
        catch (Exception ex)
        {
            _resultTextBox.Text = ex.Message;
            SetStatus("协议加载失败。");
            UpdateOverview();
        }
        finally
        {
            ToggleBusy(false, null);
        }
    }

    private void UnloadCurrentProtocol()
    {
        _messageTypeComboBox.DataSource = null;
        _filteredMessages = Array.Empty<ProtocolMessageDefinition>();
        _messageTypeSearchTextBox.Clear();
        _resultTextBox.Clear();

        if (_currentSession is null)
        {
            UpdateOverview();
            return;
        }

        _currentSession.Dispose();
        _currentSession = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        SetStatus("已清空当前协议。");
        UpdateOverview();
    }

    private void ParseCurrentMessage()
    {
        if (_currentSession is null)
        {
            SetStatus("请先加载协议。");
            return;
        }

        if (_messageTypeComboBox.SelectedItem is not ProtocolMessageDefinition definition)
        {
            SetStatus("请先选择消息类型。");
            return;
        }

        try
        {
            var bytes = ByteInputParser.Parse(_byteInputTextBox.Text);
            var message = definition.Parser.ParseFrom(bytes);

            var text = ProtoMessageFormatter.Format(message, bytes, definition.DisplayName);
            _resultTextBox.Text = text;
            SetStatus($"解析成功。字节长度: {bytes.Length}");
        }
        catch (Exception ex)
        {
            _resultTextBox.Text = ex.ToString();
            SetStatus("解析失败。");
        }
    }

    private void LoadByteFileIntoInput()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Text Files|*.txt|All Files|*.*",
            InitialDirectory = Directory.Exists(WorkspacePaths.GetBytesDirectory())
                ? WorkspacePaths.GetBytesDirectory()
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _byteInputTextBox.Text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            SetStatus($"已读取字节文件: {Path.GetFileName(dialog.FileName)}");
        }
    }

    private void ToggleBusy(bool busy, string? message)
    {
        _isBusy = busy;
        UseWaitCursor = busy;
        _refreshFilesButton.Enabled = !busy;
        _checkProtocolsButton.Enabled = !busy;
        _loadProtocolButton.Enabled = !busy;
        _unloadProtocolButton.Enabled = !busy;
        _parseButton.Enabled = !busy;
        _loadByteFileButton.Enabled = !busy;
        _protocolFileComboBox.Enabled = !busy;
        _messageTypeSearchTextBox.Enabled = !busy;
        _messageTypeComboBox.Enabled = !busy;

        if (!string.IsNullOrWhiteSpace(message))
        {
            SetStatus(message);
        }

        UpdateOverview();
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void UpdateOverview()
    {
        var selectedFile = _currentSession is not null
            ? Path.GetFileName(_currentSession.SourceFile)
            : (_protocolFileComboBox.SelectedItem as ProtocolFileOption)?.DisplayName ?? "未选择";

        _selectedProtocolValueLabel.Text = selectedFile;
        _sessionStateValueLabel.Text = _isBusy
            ? "正在处理..."
            : _currentSession is null
                ? "未加载"
                : "已加载";
        _sessionStateValueLabel.ForeColor = _isBusy
            ? Color.FromArgb(180, 83, 9)
            : _currentSession is null
                ? Color.FromArgb(71, 85, 105)
                : Color.FromArgb(21, 128, 61);

        var totalCount = _currentSession?.Messages.Count ?? 0;
        _messageCountValueLabel.Text = totalCount == 0
            ? "0"
            : $"{_filteredMessages.Count} / {totalCount}";
    }

    private void AdjustEditorSplit()
    {
        if (_editorSplit.Width <= 0)
        {
            return;
        }

        const int minPanelSize = 320;
        if (_editorSplit.Panel1MinSize != minPanelSize)
        {
            _editorSplit.Panel1MinSize = minPanelSize;
        }

        if (_editorSplit.Panel2MinSize != minPanelSize)
        {
            _editorSplit.Panel2MinSize = minPanelSize;
        }

        var availableWidth = _editorSplit.Width - _editorSplit.SplitterWidth;
        var minLeft = _editorSplit.Panel1MinSize;
        var minRight = _editorSplit.Panel2MinSize;
        var maxLeft = availableWidth - minRight;
        if (maxLeft <= minLeft)
        {
            return;
        }

        var preferredLeft = (int)(availableWidth * 0.48);
        var target = Math.Max(minLeft, Math.Min(preferredLeft, maxLeft));
        if (_editorSplit.SplitterDistance != target)
        {
            _editorSplit.SplitterDistance = target;
        }
    }

    private void CheckProtocolsOnDemand()
    {
        EnsureGeneratedProtocols(showMessageWhenNothingMissing: true);
        RefreshProtocolFiles();
    }

    private void EnsureGeneratedProtocols(bool showMessageWhenNothingMissing)
    {
        try
        {
            var missingItems = ProtocolWorkspaceSynchronizer.FindMissingGeneratedFiles();
            if (missingItems.Count == 0)
            {
                if (showMessageWhenNothingMissing)
                {
                    MessageBox.Show(this, "当前没有发现缺失的 generated 协议文件。", "重新检测", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetStatus("当前没有发现缺失的协议文件。");
                }

                return;
            }

            var message = "检测到以下 proto 文件还没有生成对应的 C# 文件：" + Environment.NewLine +
                          string.Join(Environment.NewLine, missingItems.Select(item =>
                              $"{Path.GetFileName(item.ProtoFile)} -> {Path.GetFileName(item.GeneratedFile)}")) +
                          Environment.NewLine + Environment.NewLine +
                          "是否现在自动生成？";

            var result = MessageBox.Show(
                this,
                message,
                "发现未生成协议",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                SetStatus("存在未生成的协议文件。");
                return;
            }

            ProtocolWorkspaceSynchronizer.GenerateMissingFiles(missingItems);
            SetStatus($"已自动生成 {missingItems.Count} 个协议文件。");
            RefreshProtocolFiles();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "自动生成协议失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("自动生成协议失败。");
        }
    }

    private void ApplyMessageTypeFilter()
    {
        var previousSelection = (_messageTypeComboBox.SelectedItem as ProtocolMessageDefinition)?.FullName;
        var source = _currentSession?.Messages ?? Array.Empty<ProtocolMessageDefinition>();
        var keyword = _messageTypeSearchTextBox.Text.Trim();

        _filteredMessages = source
            .Where(item => string.IsNullOrWhiteSpace(keyword) ||
                           item.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                           item.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        _messageTypeComboBox.DataSource = _filteredMessages.ToArray();
        _messageTypeComboBox.DisplayMember = nameof(ProtocolMessageDefinition.DisplayName);

        if (!string.IsNullOrWhiteSpace(previousSelection))
        {
            var index = _filteredMessages
                .Select((item, index) => new { item.FullName, index })
                .FirstOrDefault(item => string.Equals(item.FullName, previousSelection, StringComparison.OrdinalIgnoreCase))
                ?.index ?? -1;

            _messageTypeComboBox.SelectedIndex = index;
        }

        if (_messageTypeComboBox.SelectedIndex < 0 && _filteredMessages.Count > 0)
        {
            _messageTypeComboBox.SelectedIndex = 0;
        }

        if (_currentSession is not null)
        {
            SetStatus($"已加载协议: {Path.GetFileName(_currentSession.SourceFile)}，当前筛选结果 {_filteredMessages.Count} 个。");
        }

        UpdateOverview();
    }

    private static FlowLayoutPanel CreateButtonPanel()
    {
        return new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };
    }

    private static Panel CreateCard(string title, string? subtitle, out TableLayoutPanel body, bool fillBody = false)
    {
        var shell = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            Margin = new Padding(0, 0, 0, 16),
            BackColor = Color.FromArgb(218, 223, 230)
        };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = subtitle is null ? 2 : 3,
            Padding = new Padding(18),
            Margin = new Padding(0),
            BackColor = Color.White
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (subtitle is not null)
        {
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        content.RowStyles.Add(fillBody ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));

        content.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font(GetUiFont().FontFamily, 11F, FontStyle.Bold),
            Margin = new Padding(0)
        }, 0, 0);


        var bodyRow = 1;
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            content.Controls.Add(new Label
            {
                Text = subtitle,
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Margin = new Padding(0, 6, 0, 0)
            }, 0, 1);
            bodyRow = 2;
        }

        body = new TableLayoutPanel
        {
            Dock = fillBody ? DockStyle.Fill : DockStyle.Top,
            AutoSize = !fillBody,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Margin = new Padding(0, 14, 0, 0),
            BackColor = Color.White
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        content.Controls.Add(body, 0, bodyRow);
        shell.Controls.Add(content);
        return shell;
    }

    private static Control CreateInfoRow(string labelText, Label valueLabel, bool withBottomMargin = true)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = withBottomMargin ? new Padding(0, 0, 0, 12) : new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 116, 139),
            Padding = new Padding(0, 7, 0, 0),
            Margin = new Padding(0)
        }, 0, 0);
        row.Controls.Add(valueLabel, 1, 0);
        return row;
    }

    private static Control CreateStackedField(string labelText, Control inputControl, bool withBottomMargin = true)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            Margin = withBottomMargin ? new Padding(0, 0, 0, 12) : new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(CreateFieldLabel(labelText), 0, 0);
        panel.Controls.Add(inputControl, 0, 1);
        return panel;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 0, 0, 6)
        };
    }

    private static void ConfigureSummaryValueLabel(Label label)
    {
        var baseFont = GetUiFont();
        label.AutoSize = false;
        label.Dock = DockStyle.Fill;
        label.AutoEllipsis = true;
        label.MinimumSize = new Size(0, 28);
        label.TextAlign = ContentAlignment.MiddleRight;
        label.ForeColor = Color.FromArgb(15, 23, 42);
        label.Font = new Font(baseFont, FontStyle.Bold);
        label.Margin = new Padding(12, 0, 0, 0);
    }

    private static void ConfigureButton(Button button, string text, bool primary = false, bool fill = false)
    {
        button.Text = text;
        button.AutoSize = !fill;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.MinimumSize = new Size(112, 38);
        button.Margin = fill ? new Padding(4) : new Padding(6, 0, 0, 0);
        button.Padding = new Padding(12, 5, 12, 5);
        button.UseVisualStyleBackColor = true;
        button.FlatStyle = FlatStyle.Standard;
        button.FlatAppearance.BorderSize = 1;
        button.Font = primary ? new Font(GetUiFont(), FontStyle.Bold) : GetUiFont();

        if (fill)
        {
            button.Dock = DockStyle.Fill;
        }
    }

    private static Font GetUiFont()
    {
        return SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
    }
}
