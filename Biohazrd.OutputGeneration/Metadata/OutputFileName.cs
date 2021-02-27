namespace Biohazrd.OutputGeneration.Metadata
{
    public readonly struct OutputFileName : IDeclarationMetadataItem
    {
        public string FileName { get; }

        public OutputFileName(string fileName)
            => FileName = fileName;
    }
}
