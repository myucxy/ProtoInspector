ProtocolWorkspace 目录说明

proto
- 放原始 .proto 协议文件

generated
- 放由 protoc 生成的 .cs 协议文件
- 当前 ProtoInspector 默认从这个目录加载协议

bytes
- 放待解析的字节文本文件，例如 byte.txt

当前示例文件
- proto\ProtobufByCTrd.proto
- generated\ProtobufByCTrd.cs
- bytes\byte.txt
