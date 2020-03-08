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
        private static readonly string _Variable = @"\b(_?[A-Z][A-Za-z_]*|_)\b";
        private static readonly string _Term = $@"({_QuotedString}|{_AtomicString}|{_Decimal}|{_Integer}|{_Variable})";
        private static readonly string _Complex = $@"(?<functor>{_AtomicString})\s*(?<arguments>\(.*?\))\s*";
        private static readonly string _ClauseBody = $@"(?<indent>\s*)(?<body>{_Complex}|{_Term})\s*(?<token>,|\.)\s*$";
        private static readonly string _ClauseHead = $@"(?<indent>\s*)(?<head>{_Complex}|{_Term})\s*(?<token>:-|\.)\s*$";
        private static readonly string _ArgCleaner = $@"^\s*(?<arg>({_Complex}|{_Term}))\s*,.*$";

        public static readonly Regex Variable = new Regex(_Variable, DefaultOptions);
        public static readonly Regex Constant = new Regex($@"
            ( (?<string>{_QuotedString})
            | (\b
                (?<atom>{_AtomicString})
              | (?<decimal>{_Decimal})
              | (?<integer>{_Integer})
              \b) )
        ", DefaultOptions);
        public static readonly Regex Complex = new Regex($"{_Complex}$", DefaultOptions);
        public static readonly Regex QuotedString = new Regex(_QuotedString, DefaultOptions);
        public static readonly Regex ClauseHead = new Regex(_ClauseHead, DefaultOptions);
        public static readonly Regex ClauseBody = new Regex(_ClauseBody, DefaultOptions);
        public static readonly Regex ArgCleaner = new Regex(_ArgCleaner, DefaultOptions);
    }
}
