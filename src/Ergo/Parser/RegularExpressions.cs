using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Ergo.Parser
{
    public static class RegularExpressions
    {
        public static readonly RegexOptions DefaultOptions 
            = RegexOptions.Compiled 
            | RegexOptions.ExplicitCapture 
            | RegexOptions.IgnorePatternWhitespace;

        private static readonly string _AtomicString = @"[a-z_][a-z_0-9]*";
        private static readonly string _QuotedString = @"('[^']*'|""[^""]*"")";
        private static readonly string _Decimal = @"[0-9]*\.[0-9]+";
        private static readonly string _Integer = @"[0-9]+";
        private static readonly string _Variable = @"\b[A-Z][A-Za-z_]*\b";
        private static readonly string _Term = $"({_QuotedString}|{_AtomicString}|{_Decimal}|{_Integer}|{_Variable})";
        public static readonly Regex Variable = new Regex(_Variable, DefaultOptions);
        public static readonly Regex Constant = new Regex($@"
            ( (?<string>{_QuotedString})
            | (\b
                (?<atom>{_AtomicString})
              | (?<decimal>{_Decimal})
              | (?<integer>{_Integer})
              \b) )
        ", DefaultOptions);
        public static readonly Regex Complex = new Regex($@"(?<functor>{_AtomicString})\s*(?<arguments>\(\s*({_Term}\s*,?\s*)*\))", DefaultOptions);
        public static readonly Regex Argument = new Regex($@"\s*\(?(?<arg>{_Term})\s*(,|\))", DefaultOptions);
        public static readonly Regex QuotedString = new Regex(_QuotedString, DefaultOptions);
    }
}
