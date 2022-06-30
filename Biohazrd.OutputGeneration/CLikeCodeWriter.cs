#if SOURCE_GENERATOR
using System.Text;
#else
using System.IO;
#endif

namespace Biohazrd.OutputGeneration
{
#if !SOURCE_GENERATOR
    [ProvidesOutputSessionFactory]
#endif
    public class CLikeCodeWriter : CodeWriter
    {
#if !SOURCE_GENERATOR
        protected CLikeCodeWriter(OutputSession session, string filePath)
            : base(session, filePath)
        { }

        private static OutputSession.WriterFactory<CLikeCodeWriter> FactoryMethod => (session, filePath) => new CLikeCodeWriter(session, filePath);
#endif

        public IndentScope Block()
        {
            IndentScope ret = CreateIndentScope("{", "}");
            NoSeparationNeededBeforeNextLine();
            return ret;
        }

#if SOURCE_GENERATOR
        protected override void WriteOutHeaderComment(StringBuilder writer)
        { }
#else
        protected override void WriteOutHeaderComment(StreamWriter writer)
            => OutputSession.WriteHeader(writer, "// ");
#endif
    }
}
