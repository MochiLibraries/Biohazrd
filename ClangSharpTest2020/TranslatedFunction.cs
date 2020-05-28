using ClangSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace ClangSharpTest2020
{
    public sealed class TranslatedFunction
    {
        public TranslatedLibrary Library { get; }
        public FunctionDecl Function { get; }

        internal TranslatedFunction(TranslatedLibrary library, FunctionDecl function)
        {
            Library = library;
            Function = function;

            Library.AddLooseFunction(this);
        }

        public override string ToString()
            => Function.Name;
    }
}
