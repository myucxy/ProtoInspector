# Proto Inspector

Proto Inspector 是一个用于调试、解析和验证 Protobuf 消息的桌面工具。它可以动态加载由 `.proto` 生成的 C# 协议文件，将字节数据反序列化为 Protobuf JSON，也可以把 Protobuf JSON 序列化回字节格式，适合协议联调、抓包分析和接口数据排查。

## 功能特性

- 加载 `generated` 目录下的 C# Protobuf 协议文件
- 自动识别协议文件中的 Protobuf 消息类型
- 支持按关键字筛选消息类型
- 支持 Hex、Decimal、Base64 字节输入
- 支持将字节数据反序列化为 Protobuf JSON
- 支持将 Protobuf JSON 序列化为 Hex、Base64、Decimal 和 C# byte 数组
- 支持根据当前消息类型生成 JSON 样例
- 支持从文本文件读取待解析字节内容
- 支持检测 `.proto` 文件并自动生成缺失的 C# 协议文件
- 提供命令行 smoke test，便于快速验证协议解析和序列化能力

## 技术栈

- .NET 8
- Avalonia 11
- Google.Protobuf
- Microsoft.CodeAnalysis.CSharp
- protoc

## 目录结构

```text
.
├── ProtoInspector/                 # 桌面应用源码
│   ├── Models/                     # 协议会话、消息定义等模型
│   ├── Services/                   # 协议编译、字节解析、序列化等服务
│   ├── MainWindow.axaml            # Avalonia 主窗口界面
│   └── ProtoInspector.csproj       # .NET 项目文件
├── ProtocolWorkspace/              # 协议工作区
│   ├── proto/                      # 原始 .proto 文件
│   ├── generated/                  # protoc 生成的 C# 协议文件
│   └── bytes/                      # 待解析字节样例
├── protoc.exe                      # Protobuf 编译器
├── ProtoInspectorPackageUsage.txt  # 打包版本使用说明
└── package-proto-inspector-singlefile.bat
```

## 快速开始

### 运行已打包版本

如果使用 `dist/ProtoInspectorPackage` 中的打包产物，直接双击运行：

```text
ProtoInspector.exe
```

打包目录中应包含：

- `ProtoInspector.exe`
- `protoc.exe`
- `ProtocolWorkspace/`

### 从源码运行

确保本机已安装 .NET 8 SDK，然后在项目根目录执行：

```bash
dotnet run --project ProtoInspector/ProtoInspector.csproj
```

## 工作区约定

程序会自动查找 `ProtocolWorkspace` 目录：

```text
ProtocolWorkspace/
├── proto/
├── generated/
└── bytes/
```

各目录用途如下：

- `proto/`：存放原始 `.proto` 协议文件
- `generated/`：存放由 `protoc` 生成的 C# 协议文件
- `bytes/`：存放待解析的字节文本文件，例如 `byte.txt`

## 基本使用流程

1. 将 `.proto` 文件放入 `ProtocolWorkspace/proto`。
2. 如果还没有生成 C# 协议文件，点击界面上的“生成缺失文件”。
3. 在“协议文件”下拉框中选择 `generated` 目录下的 C# 文件。
4. 点击“加载协议”。
5. 在“消息类型”中搜索并选择目标 Protobuf 消息。
6. 输入 Hex、Decimal 或 Base64 字节内容，点击“反序列化”查看 JSON。
7. 输入 Protobuf JSON，点击“序列化”生成字节格式输出。

## 字节输入格式

支持自动识别以下格式：

```text
0A 44 08 CE
```

```text
0x0A 0x44 0x08 0xCE
```

```text
10 68 8 206
```

```text
CkQIzgESATE=
```

也可以显式声明格式：

```text
Hex: 0A 44 08 CE
Decimal: 10 68 8 206
Base64: CkQIzgESATE=
```

## JSON 输入提示

- 字段名遵循 Protobuf JSON 规则，通常为 `lowerCamelCase`
- `int64` / `uint64` 建议使用字符串形式，例如 `"1"`
- `bytes` 类型使用 Base64 字符串

## 命令行烟测

反序列化测试：

```bash
ProtoInspector.exe --smoke-test <generated.cs> <messageType> <byte.txt>
```

序列化测试：

```bash
ProtoInspector.exe --smoke-test --serialize <generated.cs> <messageType> <json.txt>
```

示例：

```bash
ProtoInspector.exe --smoke-test ProtocolWorkspace/generated/Test1DocumentMessage.cs Test1DocumentMessage ProtocolWorkspace/bytes/byte.txt
```

## 构建

普通构建：

```bash
dotnet build ProtoInspector/ProtoInspector.csproj
```

发布 Windows x64 单文件版本：

```bash
dotnet publish ProtoInspector/ProtoInspector.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true
```

也可以直接使用项目中的打包脚本：

```bat
package-proto-inspector-singlefile.bat
```

打包完成后，产物会输出到：

```text
dist/ProtoInspectorPackage
```

## 适用场景

- Protobuf 协议联调
- 抓包字节流分析
- 接口数据排查
- 生成和验证协议 JSON 样例
- 快速确认 `.proto` 与实际字节数据是否匹配

## License

本项目使用 [Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International](LICENSE) 协议，即 CC BY-NC-SA 4.0。

- 非商业用途可免费使用
- 商业用途需要获得作者单独授权
- 二次开发、修改或再发布时必须注明原项目和原作者
- 基于本项目的修改版本需要使用相同协议发布
