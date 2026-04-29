using System.Reflection;
using System.Runtime.Loader;
using Google.Protobuf;

namespace ProtoInspector.Services;

public sealed class CollectibleProtocolLoadContext : AssemblyLoadContext
{
    public CollectibleProtocolLoadContext() : base(isCollectible: true)
    {
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name == typeof(IMessage).Assembly.GetName().Name)
        {
            return typeof(IMessage).Assembly;
        }

        return null;
    }
}
