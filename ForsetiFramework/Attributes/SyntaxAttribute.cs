using System;

namespace ForsetiFramework
{
    public class SyntaxAttribute : Attribute
    {
        public string Syntax;

        public SyntaxAttribute(string syntax) { Syntax = syntax; }
    }
}
