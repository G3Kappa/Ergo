## Design Goals
Ergo brings logic programming to the .NET world through a lightweight and extensible Prolog implementation written entirely in C#. It is a very young project, so it's neither ISO-compliant nor production-ready. 

Its main design goal is that of extending the C# language by providing an inference engine that's easy to work with, while being efficient enough to be used in high-demand applications such as games. This focus on inter-operability means that Ergo is extremely easy to extend and tailor to each use case, at the cost of having a reduced feature set.

Ergo supports several advanced features, including:

- Libraries (C# entry points for various Ergo extensions; linked to Ergo modules)
- Last Call Optimization
- Tabling (memoization)
- Goal Expansions (macros/term rewriting)
- C# Data Bindings (marshalling of CLR objects to Ergo terms)
- Unbounded Numeric Types
- Lambdas
- Abstract Term Parsers (used to implement: Lists, Tuples, Sets, Dicts...)

Example applications include: 

- A scripting language for a game that organically "grows" into several domain specific languages for tasks such as layouting, rendering, enemy AI, level generation, etc. 
- Ergo code is naturally data-driven, a paradigm that's particularly fitting for describing large, detailed systems such as those found in games; 
- The combination of an extensible syntax and a module system allows for domain-specific representations of otherwise complex concepts, at any scale; 
- The serialization facilities of Ergo can be leveraged to store game data inside knowledge bases, making binary/JSON/XML serialization redundant; 
- The inference engine of Ergo lets you query and reason about the aforementioned data in a way that's orthogonal and complementary to idiomatic C#;
- The previous bullet points can be combined to create smart agents that are capable of learning by integrating new data into their models of the game world.

## Roadmap
At the time of writing, Ergo is fully interpreted toy language with much room for optimization. 

For a rough roadmap, please refer to: https://github.com/users/G3Kappa/projects/1/views/1
