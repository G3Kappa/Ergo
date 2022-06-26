﻿using Ergo.Lang.Ast.Terms.Interfaces;

namespace Ergo.Lang.Parser;

public interface IAbstractTermParser<A> : IAbstractTermParser
    where A : IAbstractTerm
{
    Maybe<A> TryParse(ErgoParser parser);
    Maybe<IAbstractTerm> IAbstractTermParser.Parse(ErgoParser parser) => TryParse(parser)
        .Map(some => Maybe.Some<IAbstractTerm>(some));
}
