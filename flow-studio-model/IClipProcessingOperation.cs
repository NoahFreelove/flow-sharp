namespace Flow.Studio.Model;

/// <summary>Captured control-thread transaction shared by isolated clip processors.</summary>
public interface IClipProcessingOperation
{
    GeneratorDescriptor Descriptor { get; }
    PluginPackage Package { get; }
    GenerationContext Context { get; }
    bool IsCurrent { get; }
    bool Accept(GeneratedSourceOutput result);
    void Cancel();
}
