using Avalonia.Controls;

namespace ProtoInspector;

public partial class JsonSampleEditWindow : Window
{
    private readonly TextBox _jsonTextBox;

    public JsonSampleEditWindow() : this(string.Empty)
    {
    }

    public JsonSampleEditWindow(string json)
    {
        InitializeComponent();
        _jsonTextBox = this.FindControl<TextBox>(nameof(JsonTextBox))
                       ?? throw new InvalidOperationException("未找到控件: JsonTextBox");
        _jsonTextBox.Text = json;

        var okButton = this.FindControl<Button>(nameof(OkButton))
                       ?? throw new InvalidOperationException("未找到控件: OkButton");
        var cancelButton = this.FindControl<Button>(nameof(CancelButton))
                           ?? throw new InvalidOperationException("未找到控件: CancelButton");

        okButton.Click += (_, _) => Close(_jsonTextBox.Text ?? string.Empty);
        cancelButton.Click += (_, _) => Close(null);
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
