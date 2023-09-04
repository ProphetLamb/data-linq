using System.Diagnostics;
using Microsoft.VisualBasic.CompilerServices;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
namespace Rustic.Memory.Data.Linq;

public readonly struct TableView
{
    private readonly DateTable _dataTable;

    public TableView(DateTable dataTable)
    {
        _dataTable = dataTable;
    }

    public DataTable DataTable => _dataTable;

    TableColumnView this[TableColumnQueryExpression columnQuery] { get; }
    TableColumnMutation this[TableColumnQueryExpression columnQuery] { set; }
    TableColumnQueryExpression this[string columnName] => new(columnName, StringComparer.Ordinal);
    TableColumnQueryExpression this[Func<DataColumn, bool> predicate] => new TableColumnQuery(predicate);
    TableColumnQueryExpression this[DataColumn dataColumn] => new TableColumnQuery(dataColumn);
}


public readonly struct TableColumnMutation { }

public readonly struct TableColumnView : ITableSameOriginGuarded
{
    private readonly TableColumnQueryExpression _query;

    public TableColumnView(TableColumnQueryExpression query)
    {
        _query = query;
    }

    public TableSameOriginGuard Guard => _query.Guard;

    public TableColumnView<T> As<T>()
    {
        return new TableColumnView<T>(_query);
    }
}

public readonly struct TableColumnView<T> : ITableSameOriginGuarded
{
    private readonly TableColumnQueryExpression _query;

    public TableColumnView(TableColumnQueryExpression query)
    {
        _query = query;
    }

    public TableSameOriginGuard Guard => _query.Guard;

    TableCellView<T> this[TableCellQuery<T> cellQuery] { get; }
    TableCellMutation<T> this[TableCellQuery<T> cellQuery] { set; }
}

public readonly struct TableCellQuery<T> { }

public readonly struct TableCellMutation<T> { }
public readonly struct TableCellView<T>
{

}
