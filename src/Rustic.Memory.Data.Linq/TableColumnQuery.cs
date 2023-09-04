using System.Data;
namespace Rustic.Memory.Data.Linq;

public readonly struct TableColumnQuery : ITableSameOriginGuarded
{
    private readonly TableViewSameOriginGuard _guard;
    private readonly Kind _kind;
    private readonly (string? Value, StringComparer? Comparer) _name;
    private readonly Func<DataColumn, bool>? _predicate;
    private readonly DataColumn? _dataColumn;

    public TableColumnQuery(string name, StringComparer? comparer)
    {
        _kind = Kind.Name;
        _name = (name, comparer ?? StringComparer.Ordinal);
        _predicate = default;
        _dataColumn = default;
    }

    public TableColumnQuery(Func<DataColumn, bool> predicate)
    {
        _kind = Kind.Predicate;
        _name = default;
        _predicate = predicate;
        _dataColumn = default;
    }

    public TableColumnQuery(DataColumn dataColumn)
    {
        _kind = Kind.Object;
        _name = default;
        _predicate = default;
        _dataColumn = dataColumn;
    }

    public TableViewSameOriginGuard Guard => _guard;

    public bool IsDefault => _kind == Kind.None;

    public bool Matches(DataColumn dataColumn)
    {
        return _kind switch
        {
            Kind.Name => _name.Comparer!.Equals(_name.Value!, dataColumn.Name),
            Kind.Predicate => _predicate!(dataColumn),
            Kind.Object => _dataColumn == dataColumn,
            _ => false,
        };
    }

    internal enum Kind
    {
        None = 0,
        Name,
        Predicate,
        Object,
    }
}

public readonly struct TableColumnQueryExpression : ITableSameOriginGuarded
{
    // _queryFist is the first node, a list of operators with rhs nodes follows
    private readonly TableColumnQuery _queryFirst;
    private readonly ImmutableAppendOnlyArray<(LogicOperator Operator, TableColumnQuery Query)> _queriesWithOperators;

    public TableColumnQueryExpression(TableColumnQuery query)
    {
        _queryFirst = query;
        _queriesWithOperators = default;
    }

    private TableColumnQueryExpression(TableColumnQuery queryFirst, ImmutableAppendOnlyArray<(LogicOperator Operator, TableColumnQuery Query)> queriesWithOperators)
    {
        _queryFirst = queryFirst;
        _queriesWithOperators = queriesWithOperators;
    }

    public TableSameOriginGuard Guard => _queryFirst.Guard;

    public bool Matches(DataColumn dataColumn)
    {
        if (_queryFirst.IsDefault)
        {
            return false;
        }

        var parser = new RecursiveDescentParser(new(_queryFirst, _queriesWithOperators), dataColumn);
        return parser.ParseInternal(parser.Advance(false), 0);
    }

    public static implicit operator TableColumnQueryExpression(TableColumnQuery query) => new(query);

    private TableColumnQueryExpression Append(LogicOperator op, TableColumnQuery query)
    {
        _queryFirst.Guard.AssertSameSource(query.Guard);
        if (_queryFirst.IsDefault)
        {
            return new(query);
        }

        return new(_queryFirst, _queriesWithOperators.Add((op, query)));
    }

    private TableColumnQueryExpression Concat(LogicOperator op, TableColumnQueryExpression lhs)
    {
        if (_queryFirst.IsDefault)
        {
            return lhs;
        }

        if (lhs._queryFirst.IsDefault)
        {
            return this;
        }

        return new(_queryFirst, _queriesWithOperators.Add((op, lhs._queryFirst)).AddRange(lhs._queriesWithOperators));
    }

    public static TableColumnQueryExpression operator &(TableColumnQueryExpression left, TableColumnQueryExpression right) => left.Concat(LogicOperator.And, right);
    public static TableColumnQueryExpression operator |(TableColumnQueryExpression left, TableColumnQueryExpression right) => left.Concat(LogicOperator.Or, right);

    internal ref struct Enumerator
    {
        private readonly TableColumnQuery _queryFirst;
        private readonly ImmutableAppendOnlyArray<(LogicOperator Operator, TableColumnQuery Query)> _queriesWithOperators;
        private int _index;

        public Enumerator(TableColumnQuery queryFirst, ImmutableAppendOnlyArray<(LogicOperator Operator, TableColumnQuery Query)> queriesWithOperators)
        {
            _queryFirst = queryFirst;
            _queriesWithOperators = queriesWithOperators;
            _index = -1;
        }

        public readonly (LogicOperator Operator, TableColumnQuery Query) Current
        {
            get
            {
                if (_index == -1)
                {
                    return (LogicOperator.None, _queryFirst);
                }
                else
                {
                    return _queriesWithOperators[_index];
                }
            }
        }

        [Pure]
        public bool MoveNext()
        {
            if (_index == -1)
            {
                _index = 0;
                return !_queryFirst.IsDefault;
            }
            if (_index < _queriesWithOperators.Length)
            {
                _index++;
                return true;
            }

            return false;
        }

        [Pure]
        public readonly bool PeekNext([MaybeNullWhen(false)] out (LogicOperator Operator, TableColumnQuery Query) next)
        {
            if (_index == -1)
            {
                next = (LogicOperator.None, _queryFirst);
                return !_queryFirst.IsDefault;
            }
            if (_index < _queriesWithOperators.Length)
            {
                next = _queriesWithOperators[_index];
                return true;
            }

            next = default;
            return false;
        }
    }

    internal ref struct RecursiveDescentParser
    {
        Enumerator _source;
        DataColumn _dataColumn;

        public RecursiveDescentParser(Enumerator source, DataColumn dataColumn)
        {
            _source = source;
            _dataColumn = dataColumn;
        }

        // TODO: build expression tree greedily

        internal bool Advance(bool lhs)
        {
            bool moved = _source.MoveNext();
            Debug.Assert(moved);
            var (op, query) = _source.Current;
            return op.Evaluate(lhs, query.Matches(_dataColumn));
        }

        internal bool ParseInternal(bool lhs, int minimumPrecedence)
        {
            while (_source.PeekNext(out var lookahead) && lookahead.Operator.GetPrecedence() >= minimumPrecedence)
            {
                var op = lookahead.Operator;
                var rhs = Advance(lhs);
                while (_source.PeekNext(out lookahead) && lookahead.Operator > op)
                {
                    rhs = ParseInternal(rhs, lookahead.Operator.GetPrecedence() + (lookahead.Operator.IsRightAssociative() ? 0 : 1));
                }
                lhs = op.Evaluate(lhs, rhs);
            }
            return lhs;
        }
    }
}

/// <summary>
/// Binary operator, value is the precedence.
/// </summary>
internal enum LogicOperator
{
    None = 0,
    And,
    Or,
}

internal static class LogicOperatorExtensions
{
    public static int GetPrecedence(this LogicOperator op) => op switch
    {
        LogicOperator.Or => 2,
        LogicOperator.And => 1,
        _ => 0,
    };

    public static bool IsRightAssociative(this LogicOperator op) => op switch
    {
        LogicOperator.Or => false,
        LogicOperator.And => false,
        _ => false,
    };

    public static bool Evaluate(this LogicOperator op, bool lhs, bool rhs)
    {
        return op switch
        {
            LogicOperator.Or => lhs | rhs,
            LogicOperator.And => lhs & rhs,
            _ => rhs,
        };
    }
}
