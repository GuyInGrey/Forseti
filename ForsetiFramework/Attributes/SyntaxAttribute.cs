using System;

namespace ForsetiFramework
{
    [AttributeUsage(AttributeTargets.Method)]
    public class SyntaxAttribute : Attribute
    {
        public string Syntax;

        public SyntaxAttribute(string syntax) { Syntax = syntax; }
    }
}
