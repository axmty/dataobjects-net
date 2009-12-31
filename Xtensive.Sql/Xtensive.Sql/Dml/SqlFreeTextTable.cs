// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2009.12.29

using System;
using System.Linq;
using System.Collections.Generic;
using Xtensive.Core.Collections;
using Xtensive.Sql.Model;

namespace Xtensive.Sql.Dml
{
  [Serializable]
  public class SqlFreeTextTable : SqlTable
  {
    public SqlTableRef TargetTable { get; private set; }

    public SqlTableColumnCollection TargetColumns { get; private set; }

    public SqlExpression FreeText { get; private set; }

    internal override object Clone(SqlNodeCloneContext context)
    {
      throw new NotImplementedException();
    }

    public override void AcceptVisitor(ISqlVisitor visitor)
    {
      visitor.Visit(this);
    }


    // Constructors

    internal SqlFreeTextTable(DataTable dataTable, SqlExpression freeText)
      : this(dataTable, freeText, ArrayUtils<string>.EmptyArray)
    {
    }

    internal SqlFreeTextTable(DataTable dataTable, SqlExpression freeText, params string[] columnNames)
      : base(string.Empty)
    {
      TargetTable = SqlDml.TableRef(dataTable);
      FreeText = freeText;
      var tableColumns = new List<SqlTableColumn>();
      if (columnNames.Length == 0)
        tableColumns.Add(Asterisk);
      else
        tableColumns = columnNames.Select(cn => SqlDml.TableColumn(this, cn)).ToList();
      TargetColumns = new SqlTableColumnCollection(tableColumns);
    }
  }
}