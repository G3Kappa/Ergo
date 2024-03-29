![example workflow](https://github.com/G3Kappa/Ergo/actions/workflows/dotnet.yml/badge.svg)
[![license](https://img.shields.io/badge/License-AGPL-purple.svg)](LICENSE)

## Design Goals
Ergo brings first-order logic to the .NET world through a lightweight and extensible Prolog implementation written entirely in C#. It is a relatively young project, so it's neither ISO-compliant nor stable, but it's been consistently improving over the past few years. 

Its main design goals are to be flexible and customizable, to handle interop with C# seamlessly, and to be efficient enough to be worthwhile as a scripting language in high-demand applications such as games.
Thanks to its versatile syntax and extensible architecture, Ergo can be adapted to any use case and lends itself well to the creation of domain-specific languages. 
Unification allows for very complex pattern-matching, and users can even implement their own parsers for their own *abstract types* that override standard unification, or add their own built-ins.

Ergo already supports several advanced features, including:

- Compilation (Ergo targets a VM -- the ErgoVM)
- Libraries (C# entry points for various Ergo extensions; linked to Ergo modules)
- Tail Call Optimization (for the execution of tail recursive predicates)
- Predicate Expansions (macros/term rewriting)
- Tabling (memoization)
- Abstract Terms & Abstract Term Parsers (for custom types implemented on top of canonical terms)
    - Dictionaries (akin to SWI-Prolog)
    - Ordered Sets
    - Lists
    - Tuples (comma-lists)
- Marshalling of CLR objects to/from Ergo terms (both complex-style and dictionary-style)
- Unbounded Numeric Types (i.e. BigDecimal as the underlying numeric type)
    - In the future, optimizations for integer and float arithmetic could be added, but performance-critical codepaths can be delegated to C#
- Lambdas & Higher-Kinded Predicates 
- Dynamic Predicates

## Roadmap
At the time of writing, Ergo is a ~~fully interpreted~~ **partially compiled** toy language with much room for optimization. 

For a rough roadmap, please refer to: https://github.com/users/G3Kappa/projects/1/views/1
