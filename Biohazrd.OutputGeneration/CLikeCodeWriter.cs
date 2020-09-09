using System.IO;

namespace Biohazrd.OutputGeneration
{
    [ProvidesOutputSessionFactory]
    public class CLikeCodeWriter : CodeWriter
    {
        protected CLikeCodeWriter(OutputSession session, string filePath)
            : base(session, filePath)
        { }

        private static OutputSession.WriterFactory<CLikeCodeWriter> FactoryMethod => (session, filePath) => new CLikeCodeWriter(session, filePath);

        public IndentScope Block()
        {
            IndentScope ret = CreateIndentScope("{", "}");
            NoSeparationNeededBeforeNextLine();
            return ret;
        }

        protected override void WriteOutHeaderComment(StreamWriter writer)
            => OutputSession.WriteHeader(writer, "// ");
    }
}
