// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xtensive.Core;
using Xtensive.Collections;
using Xtensive.Sql.Compiler;
using Xtensive.Sql.Info;
using Xtensive.Sql.Model;
using Xtensive.Sql.Ddl;
using Xtensive.Sql.Dml;
using Xtensive.Sql.Drivers.SqlServerCe.Resources;
using Xtensive.Sql;

namespace Xtensive.Sql.Drivers.SqlServerCe.v3_5
{
  internal class Translator : SqlTranslator
  {
    public override string DateTimeFormatString { get { return @"'cast ('\'yyyy\-MM\-dd HH\:mm\:ss\.fff\'' as datetime)'"; } }
    public override string TimeSpanFormatString { get { return string.Empty; } }
    public override string FloatFormatString { get { return "'cast('" + base.FloatFormatString  + "'e0 as real')"; } }
    public override string DoubleFormatString { get { return "'cast('" + base.DoubleFormatString + "'e0 as float')"; } }

    public override void Initialize()
    {
      base.Initialize();
      FloatNumberFormat.NumberDecimalSeparator = ".";
      DoubleNumberFormat.NumberDecimalSeparator = ".";
    }

    public override string Translate(SqlCompilerContext context, SqlFunctionCall node, FunctionCallSection section, int position)
    {
      if (node.FunctionType == SqlFunctionType.LastAutoGeneratedId) {
        if (section == FunctionCallSection.Entry)
          return Translate(node.FunctionType);
        if (section == FunctionCallSection.Exit)
          return string.Empty;
      }
      switch (section) {
      case FunctionCallSection.ArgumentEntry:
        return string.Empty;
      case FunctionCallSection.ArgumentDelimiter:
        return ArgumentDelimiter;
      default:
        return base.Translate(context, node, section, position);
      }
    }

    public override string Translate(SqlFunctionType functionType)
    {
      switch (functionType) {
      case SqlFunctionType.IntervalAbs:
        return "ABS";
      case SqlFunctionType.IntervalNegate:
        return "-";
      case SqlFunctionType.CurrentDate:
        return "GETDATE";
      case SqlFunctionType.CharLength:
        return "LEN";
      case SqlFunctionType.BinaryLength:
        return "DATALENGTH";
      case SqlFunctionType.Position:
        return "CHARINDEX";
      case SqlFunctionType.Atan2:
        return "ATN2";
      case SqlFunctionType.LastAutoGeneratedId:
        return "@@IDENTITY";
      }
      return base.Translate(functionType);
    }

    public override string Translate(SqlNodeType type)
    {
      switch (type) {
      case SqlNodeType.Count:
        return "COUNT";
      case SqlNodeType.Concat:
        return "+";
      case SqlNodeType.Overlaps:
        throw new NotSupportedException(string.Format(Strings.ExOperationXIsNotSupported, type));
      case SqlNodeType.Intersect:
      case SqlNodeType.Except:
        throw SqlHelper.NotSupported(type.ToString());
      }
      return base.Translate(type);
    }

    public override string Translate(SqlCompilerContext context, TableColumn column, TableColumnSection section)
    {
      switch (section) {
      case TableColumnSection.Type:
        return column.Domain==null
          ? Translate(column.DataType)
          : QuoteIdentifier(column.Domain.Schema.DbName, column.Domain.DbName);
      case TableColumnSection.GenerationExpressionEntry:
        return "AS (";
      case TableColumnSection.GeneratedEntry:
      case TableColumnSection.GeneratedExit:
      case TableColumnSection.SetIdentityInfoElement:
      case TableColumnSection.Exit:
        return string.Empty;
      default:
        return base.Translate(context, column, section);
      }
    }

    public override string Translate(SqlCompilerContext context, SqlAlterTable node, AlterTableSection section)
    {
      switch (section) {
      case AlterTableSection.AddColumn:
        return "ADD";
      case AlterTableSection.DropBehavior:
        return string.Empty;
      default:
        return base.Translate(context, node, section);
      }
    }

    public override string Translate(SqlCompilerContext context, SequenceDescriptor descriptor,
                                     SequenceDescriptorSection section)
    {
      switch (section)
      {
        case SequenceDescriptorSection.StartValue:
        case SequenceDescriptorSection.RestartValue:
          if (descriptor.StartValue.HasValue)
            return "IDENTITY (" + descriptor.StartValue.Value + RowItemDelimiter;
          return String.Empty;
        case SequenceDescriptorSection.Increment:
          if (descriptor.Increment.HasValue)
            return descriptor.Increment.Value + ")";
          return String.Empty;
        default:
          return String.Empty;
      }
    }

    public override string Translate(SqlCompilerContext context, Constraint constraint, ConstraintSection section)
    {
      switch (section)
      {
        case ConstraintSection.Exit:
          ForeignKey fk = constraint as ForeignKey;
          if (fk != null)
          {
            if (fk.OnUpdate == ReferentialAction.Cascade)
              return ") ON UPDATE CASCADE";
            if (fk.OnDelete == ReferentialAction.Cascade)
              return ") ON DELETE CASCADE";
          }
          return ")";
        default:
          return base.Translate(context, constraint, section);
      }
    }

    public override string Translate(SqlCompilerContext context, SqlCreateTable node, CreateTableSection section)
    {
      switch (section) {
      case CreateTableSection.Entry:
        var builder = new StringBuilder();
        builder.Append("CREATE ");
        var temporaryTable = node.Table as TemporaryTable;
        if (temporaryTable!=null) {
          if (temporaryTable.IsGlobal)
            temporaryTable.DbName = "##" + temporaryTable.Name;
          else
            temporaryTable.DbName = "#" + temporaryTable.Name;
        }
        builder.Append("TABLE " + Translate(context, node.Table));
        return builder.ToString();
      case CreateTableSection.Exit:
        string result = string.IsNullOrEmpty(node.Table.Filegroup)
          ? string.Empty
          : " ON " + QuoteIdentifier(node.Table.Filegroup);
        return result;
      }
      return base.Translate(context, node, section);
    }

    public override string Translate(SqlCompilerContext context, SqlCreateView node, NodeSection section)
    {
      switch (section)
      {
        case NodeSection.Exit:
          if (node.View.CheckOptions == CheckOptions.Cascaded)
            return "WITH CHECK OPTION";
          else
            return string.Empty;
        default:
          return base.Translate(context, node, section);
      }
    }

    public override string Translate(SqlCompilerContext context, SqlCreateDomain node, CreateDomainSection section)
    {
      switch (section) {
      case CreateDomainSection.Entry:
        return string.Format("CREATE TYPE {0} FROM {1}", Translate(context, node.Domain), Translate(node.Domain.DataType));
      default:
        return string.Empty;
      }
    }

    public override string Translate(SqlCompilerContext context, SqlDropDomain node)
    {
      return string.Format("DROP TYPE {0}", Translate(context, node.Domain));
    }
    
    public override string Translate(SqlCompilerContext context, SqlDropIndex node)
    {
      return string.Format("DROP INDEX {0}.{1}", QuoteIdentifier(node.Index.DataTable.DbName), QuoteIdentifier(node.Index.DbName));
    }

    public override string Translate(SqlCompilerContext context, SqlAlterDomain node, AlterDomainSection section)
    {
      throw SqlHelper.NotSupported("ALTER DOMAIN"); // NOTE: Do not localize, it's an SQL keyword
    }

    public override string Translate(SqlCompilerContext context, SqlDeclareCursor node, DeclareCursorSection section)
    {
      if (section==DeclareCursorSection.Holdability || section==DeclareCursorSection.Returnability)
        return string.Empty;
      return base.Translate(context, node, section);
    }

    public override string Translate(SqlCompilerContext context, SqlJoinExpression node, JoinSection section)
    {
      switch (section) {
        case JoinSection.Specification:
          if (node.Expression==null)
            switch (node.JoinType) {
            case SqlJoinType.InnerJoin:
            case SqlJoinType.LeftOuterJoin:
            case SqlJoinType.RightOuterJoin:
            case SqlJoinType.FullOuterJoin:
              throw new NotSupportedException();
            case SqlJoinType.CrossApply:
              return "CROSS APPLY";
            case SqlJoinType.LeftOuterApply:
              return "OUTER APPLY";
             }
          var joinHint = TryFindJoinHint(context, node);
          return Translate(node.JoinType)
            + (joinHint != null ? " " + Translate(joinHint.Method) : string.Empty) + " JOIN";
      }
      return base.Translate(context, node, section);
    }

    public override string Translate(SqlCompilerContext context, SqlQueryExpression node, QueryExpressionSection section)
    {
      if (node.All && section == QueryExpressionSection.All && (node.NodeType == SqlNodeType.Except || node.NodeType == SqlNodeType.Intersect))
        return string.Empty;
      return base.Translate(context, node, section);
    }

    private static SqlJoinHint TryFindJoinHint(SqlCompilerContext context, SqlJoinExpression node)
    {
      SqlQueryStatement statement = null;
      for (int i = 0, count = context.GetTraversalPath().Length; i < count; i++) {
        if (context.GetTraversalPath()[i] is SqlQueryStatement)
          statement = context.GetTraversalPath()[i] as SqlQueryStatement;
      }
      if (statement==null || statement.Hints.Count==0)
        return null;
      var candidate = statement.Hints
        .OfType<SqlJoinHint>()
        .FirstOrDefault(hint => hint.Table==node.Right);
      return candidate;
    }

    public override string Translate(SqlJoinMethod method)
    {
      switch (method) {
      case SqlJoinMethod.Hash:
        return "HASH";
      case SqlJoinMethod.Merge:
        return "MERGE";
      case SqlJoinMethod.Loop:
        return "LOOP";
      case SqlJoinMethod.Remote:
        return "REMOTE";
      default:
        return string.Empty;
      }
    }

    public override string Translate(SqlCompilerContext context, SqlSelect node, SelectSection section)
    {
      switch (section) {
      case SelectSection.Limit:
        return "TOP";
      case SelectSection.Offset:
        throw new NotSupportedException();
      case SelectSection.Exit:
        if (node.Hints.Count==0)
          return string.Empty;
        var hints = new List<string>(node.Hints.Count);
        foreach (var hint in node.Hints) {
          if (hint is SqlForceJoinOrderHint)
            hints.Add("FORCE ORDER");
          else if (hint is SqlFastFirstRowsHint)
            hints.Add("FAST " + (hint as SqlFastFirstRowsHint).Amount);
          else if (hint is SqlNativeHint)
            hints.Add((hint as SqlNativeHint).HintText);
        }
        return hints.Count > 0 ? "OPTION (" + string.Join(", ", hints.ToArray()) + ")" : string.Empty;
      }

      return base.Translate(context, node, section);
    }

    public override string Translate(SqlCompilerContext context, SqlRenameTable node)
    {
      return string.Format("EXEC sp_rename '{0}', '{1}'", Translate(context, node.Table), node.NewName);
    }

    public virtual string Translate(SqlCompilerContext context, SqlRenameColumn action)
    {
      string schemaName = action.Column.Table.Schema.DbName;
      string tableName = action.Column.Table.DbName;
      string columnName = action.Column.DbName;
      return string.Format("EXEC sp_rename '{0}', '{1}', 'COLUMN'",
        QuoteIdentifier(schemaName, tableName, columnName), action.NewName);
    }
    
    public override string Translate(SqlCompilerContext context, SqlExtract node, ExtractSection section)
    {
      switch (section) {
      case ExtractSection.Entry:
        return "DATEPART(";
      case ExtractSection.From:
        return ",";
      default:
        return base.Translate(context, node, section);
      }
    }

    public override string Translate(SqlCompilerContext context, SqlTableRef node, TableSection section)
    {
      var reference = base.Translate(context, node, section);
      if (section!=TableSection.AliasDeclaration)
        return reference;
      var select = context.GetTraversalPath()
        .OfType<SqlSelect>()
        .Where(s => s.Lock!=SqlLockType.Empty)
        .FirstOrDefault();
      return select==null ? reference : string.Format("{0} WITH ({1})", reference, Translate(select.Lock));
    }

    public override string Translate(SqlCompilerContext context, SqlTrim node, TrimSection section)
    {
      switch (section) {
      case TrimSection.Entry:
        switch (node.TrimType) {
        case SqlTrimType.Leading:
          return "LTRIM(";
        case SqlTrimType.Trailing:
          return "RTRIM(";
        case SqlTrimType.Both:
          return "LTRIM(RTRIM(";
        default:
          throw new ArgumentOutOfRangeException();
        }
      case TrimSection.Exit:
        switch (node.TrimType) {
        case SqlTrimType.Leading:
        case SqlTrimType.Trailing:
          return ")";
        case SqlTrimType.Both:
          return "))";
        default:
          throw new ArgumentOutOfRangeException();
        }
      default:
        throw new ArgumentOutOfRangeException();
      }
    }

    public override string Translate(SqlCompilerContext context, SqlDropSchema node)
    {
      return "DROP SCHEMA " + QuoteIdentifier(node.Schema.DbName);
    }

    public override string Translate(SqlCompilerContext context, SqlDropTable node)
    {
      return "DROP TABLE " + Translate(context, node.Table);
    }

    public override string Translate(SqlCompilerContext context, SqlDropView node)
    {
      return "DROP VIEW " + Translate(context, node.View);
    }

    public override string Translate(SqlTrimType type)
    {
      return string.Empty;
    }

    public override string Translate(SqlCompilerContext context, object literalValue)
    {
      var literalType = literalValue.GetType();
      if (literalType==typeof (TimeSpan))
        return Convert.ToString((long) ((TimeSpan) literalValue).Ticks*100);
      if (literalType==typeof (Boolean))
        return ((bool) literalValue) ? "cast(1 as bit)" : "cast(0 as bit)";
      if (literalType==typeof(DateTime)) {
        var dateTime = (DateTime) literalValue;
        var dateTimeRange = (ValueRange<DateTime>) Driver.ServerInfo.DataTypes.DateTime.ValueRange;
        var newValue = ValueRangeValidator.Correct(dateTime, dateTimeRange);
        return newValue.ToString(DateTimeFormatString);
      }
      if (literalType==typeof(byte[])) {
        var array = (byte[]) literalValue;
        var builder = new StringBuilder(2 * (array.Length + 1));
        builder.Append("0x");
        builder.AppendHexArray(array);
        return builder.ToString();
      }
      if (literalType==typeof(Guid))
        return QuoteString(literalValue.ToString());
      if (literalType==typeof (Int64))
        return String.Format("CAST({0} as BIGINT)", literalValue);
      return base.Translate(context, literalValue);
    }
    
    public override string Translate(SqlLockType lockType)
    {
      var items = new List<string>();
      items.Add("ROWLOCK");
      if (lockType.Supports(SqlLockType.Update))
        items.Add("UPDLOCK");
      else if (lockType.Supports(SqlLockType.Exclusive))
        items.Add("XLOCK");
      if (lockType.Supports(SqlLockType.ThrowIfLocked))
        items.Add("NOWAIT");
      else if (lockType.Supports(SqlLockType.SkipLocked))
        items.Add("READPAST");
      return items.ToCommaDelimitedString();
    }

    public override string Translate(Collation collation)
    {
      return collation.DbName;
    }


    // Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Translator"/> class.
    /// </summary>
    /// <param name="driver">The driver.</param>
    protected internal Translator(SqlDriver driver)
      : base(driver)
    {
    }
  }
}