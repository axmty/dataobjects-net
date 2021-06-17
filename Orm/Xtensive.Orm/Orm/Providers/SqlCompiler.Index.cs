// Copyright (C) 2009-2010 Xtensive LLC.
// This code is distributed under MIT license terms.
// See the License.txt file in the project root for more information.
// Created by: Denis Krjuchkov
// Created:    2009.11.13

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xtensive.Core;
using Xtensive.Linq;
using Xtensive.Orm.Internals;
using Xtensive.Orm.Model;
using Xtensive.Orm.Rse.Providers;
using Xtensive.Reflection;
using Xtensive.Sql;
using Xtensive.Sql.Dml;
using IndexInfo = Xtensive.Orm.Model.IndexInfo;

namespace Xtensive.Orm.Providers
{
  partial class SqlCompiler 
  {
    protected struct QueryAndBindings
    {
      public SqlSelect Query { get; set; }

      public IEnumerable<QueryParameterBinding> Bindings { get; set; }

    }

    protected override SqlProvider VisitFreeText(FreeTextProvider provider)
    {
      throw new NotSupportedException();
    }

    protected override SqlProvider VisitContainsTable(ContainsTableProvider provider)
    {
      throw new NotSupportedException();
    }

    /// <inheritdoc/>
    protected override SqlProvider VisitIndex(IndexProvider provider)
    {
      var index = provider.Index.Resolve(Handlers.Domain.Model);
      var queryAndBindings = BuildProviderQuery(index);
      return CreateProvider(queryAndBindings.Query, queryAndBindings.Bindings, provider);
    }

    protected QueryAndBindings BuildProviderQuery(IndexInfo index)
    {
      if (index.IsVirtual) {
        if ((index.Attributes & IndexAttributes.Union) > 0)
          return BuildUnionQuery(index);
        if ((index.Attributes & IndexAttributes.Join) > 0)
          return BuildJoinQuery(index);
        if ((index.Attributes & IndexAttributes.Filtered) > 0)
          return BuildFilteredQuery(index);
        if ((index.Attributes & IndexAttributes.View) > 0)
          return BuildViewQuery(index);
        if ((index.Attributes & IndexAttributes.Typed) > 0)
          return BuildTypedQuery(index);
        throw new NotSupportedException(String.Format(Strings.ExUnsupportedIndex, index.Name, index.Attributes));
      }
      return BuildTableQuery(index);
    }

    private QueryAndBindings BuildTableQuery(IndexInfo index)
    {
      var domainHandler = Handlers.DomainHandler;
      var table = Mapping[index.ReflectedType];

      var atRootPolicy = false;

      if (table==null) {
        table = Mapping[index.ReflectedType.GetRoot()];
        atRootPolicy = true;
      }

      SqlSelect query;
      if (!atRootPolicy) {
        var tableRef = SqlDml.TableRef(table);
        query = SqlDml.Select(tableRef);
        query.Columns.AddRange(index.Columns.Select(c => tableRef[c.Name]));
      }
      else {
        var root = index.ReflectedType.GetRoot().AffectedIndexes.First(i => i.IsPrimary);
        var lookup = root.Columns.ToDictionary(c => c.Field, c => c.Name);
        var tableRef = SqlDml.TableRef(table);
        query = SqlDml.Select(tableRef);
        query.Columns.AddRange(index.Columns.Select(c => tableRef[lookup[c.Field]]));
      }
      return new QueryAndBindings { Query = query, Bindings = Enumerable.Empty<QueryParameterBinding>() };
    }

    private QueryAndBindings BuildUnionQuery(IndexInfo index)
    {
      ISqlQueryExpression result = null;
      IEnumerable<QueryParameterBinding> resultBindings = null;

      var baseQueries = index.UnderlyingIndexes.Select(BuildProviderQuery).ToList();
      foreach (var select in baseQueries) {
        result = result==null
          ? (ISqlQueryExpression) select.Query
          : result.Union(select.Query);
        resultBindings = (resultBindings == null)
          ? select.Bindings
          : resultBindings.Union(select.Bindings);
      }

      var unionRef = SqlDml.QueryRef(result);
      var query = SqlDml.Select(unionRef);
      query.Columns.AddRange(unionRef.Columns);

      return new QueryAndBindings { Query = query, Bindings = resultBindings };
    }

    private QueryAndBindings BuildJoinQuery(IndexInfo index)
    {
      SqlTable resultTable = null;
      SqlTable rootTable = null;

      int keyColumnCount = index.KeyColumns.Count;
      var underlyingQueries = index.UnderlyingIndexes.Select(BuildProviderQuery);

      //var sourceTables = index.UnderlyingIndexes.Any(i => i.IsVirtual)
      //  ? underlyingQueries.Select(q =>SqlDml.QueryRef(q.Query)).Cast<SqlTable>().ToList()
      //  : underlyingQueries.Select(
      //    q => {
      //      var tableRef = (SqlTableRef) q.From;
      //      return (SqlTable) SqlDml.TableRef(tableRef.DataTable, tableRef.Name, q.Columns.Select(c => c.Name));
      //  }).ToList();

      List<SqlTable> sourceTables = new List<SqlTable>();
      IEnumerable<QueryParameterBinding> resultBindings = null;
      if(index.UnderlyingIndexes.Any(i => i.IsVirtual)) {
        foreach(var item in underlyingQueries) {
          sourceTables.Add(SqlDml.QueryRef(item.Query));
          resultBindings = (resultBindings == null)
            ? item.Bindings
            : resultBindings.Union(item.Bindings);
        }
      }
      else {
        foreach (var item in underlyingQueries) {
          var tableRef = (SqlTableRef) item.Query.From;
          sourceTables.Add(SqlDml.TableRef(tableRef.DataTable, tableRef.Name, item.Query.Columns.Select(c => c.Name)));
          resultBindings = (resultBindings == null)
            ? item.Bindings
            : resultBindings.Union(item.Bindings);
        }
      }

      foreach (var table in sourceTables) {
        if (resultTable==null)
          resultTable = rootTable = table;
        else {
          SqlExpression joinExpression = null;
          for (int i = 0; i < keyColumnCount; i++) {
            var binary = (table.Columns[i]==rootTable.Columns[i]);
            if (joinExpression.IsNullReference())
              joinExpression = binary;
            else
              joinExpression &= binary;
          }
          resultTable = resultTable.InnerJoin(table, joinExpression);
        }
      }

      var columns = new List<SqlColumn>();
      foreach (var map in index.ValueColumnsMap) {
        var table = sourceTables[map.First];
        if (columns.Count==0) {
          var keyColumns = Enumerable
            .Range(0, keyColumnCount)
            .Select(i => table.Columns[i])
            .Cast<SqlColumn>();
          columns.AddRange(keyColumns);
        }
        var valueColumns = map.Second
          .Select(columnIndex => table.Columns[columnIndex + keyColumnCount])
          .Cast<SqlColumn>();
        columns.AddRange(valueColumns);
      }

      var query = SqlDml.Select(resultTable);
      query.Columns.AddRange(columns);

      return new QueryAndBindings { Query = query, Bindings = resultBindings };
    }

    private QueryAndBindings BuildFilteredQuery(IndexInfo index)
    {
      var underlyingIndex = index.UnderlyingIndexes[0];
      var baseQueryAndBindings = BuildProviderQuery(underlyingIndex);
      var baseQuery = baseQueryAndBindings.Query;
      var bindings = baseQueryAndBindings.Bindings;

      SqlExpression filter = null;
      var type = index.ReflectedType;
      var discriminatorMap = type.Hierarchy.TypeDiscriminatorMap;
      var filterByTypes = index.FilterByTypes.ToList();
      if (underlyingIndex.IsTyped && discriminatorMap != null) {
        var columnType = discriminatorMap.Column.ValueType;
        var discriminatorColumnIndex = underlyingIndex.Columns
          .Where(c => !c.Field.IsTypeId)
          .Select((c,i) => new {c,i})
          .Where(p => p.c == discriminatorMap.Column)
          .Single().i;
        var discriminatorColumn = baseQuery.From.Columns[discriminatorColumnIndex];
        var containsDefault = filterByTypes.Contains(discriminatorMap.Default);
        var values = filterByTypes
          .Select(t => GetDiscriminatorValue(discriminatorMap, t.TypeDiscriminatorValue));
        if (filterByTypes.Count == 1) {
          var discriminatorValue = GetDiscriminatorValue(discriminatorMap, filterByTypes.First().TypeDiscriminatorValue);
          filter = discriminatorColumn == SqlDml.Literal(discriminatorValue);
        }
        else {
          filter = SqlDml.In(discriminatorColumn, SqlDml.Array(values));
          if (containsDefault) {
            var allValues = discriminatorMap
              .Select(p => GetDiscriminatorValue(discriminatorMap, p.First));
            filter |= SqlDml.NotIn(discriminatorColumn, SqlDml.Array(allValues));
          }
        }
      }
      else {
        var typeIdColumn = baseQuery.Columns[Handlers.Domain.Handlers.NameBuilder.TypeIdColumnName];
        var typeIds = filterByTypes.Select(t => TypeIdRegistry[t]).ToArray();
        filter = filterByTypes.Count == 1
          ? typeIdColumn == TypeIdRegistry[filterByTypes.First()]
          : SqlDml.In(typeIdColumn, SqlDml.Array(typeIds));
      }
      var query = SqlDml.Select(baseQuery.From);
      query.Columns.AddRange(baseQuery.Columns);
      query.Where = filter;

      baseQueryAndBindings.Query = query;
      baseQueryAndBindings.Bindings = bindings.Union(Enumerable.Empty<QueryParameterBinding>());
      return baseQueryAndBindings;
    }

    private QueryAndBindings BuildViewQuery(IndexInfo index)
    {
      var underlyingIndex = index.UnderlyingIndexes[0];
      var baseQueryAndBindings = BuildProviderQuery(underlyingIndex);
      var baseQuery = baseQueryAndBindings.Query;
      var baseBindings = baseQueryAndBindings.Bindings;

      var query = SqlDml.Select(baseQuery.From);
      query.Where = baseQuery.Where;
      query.Columns.AddRange(index.SelectColumns.Select(i => baseQuery.Columns[i]));

      baseQueryAndBindings.Query = query;

      return baseQueryAndBindings;
    }

    private QueryAndBindings BuildTypedQuery(IndexInfo index)
    {
      var underlyingIndex = index.UnderlyingIndexes[0];
      var baseQueryAndBindings = BuildProviderQuery(underlyingIndex);
      var baseQuery = baseQueryAndBindings.Query;
      var baseBindings = baseQueryAndBindings.Bindings;
      var query = SqlDml.Select(baseQuery.From);
      query.Where = baseQuery.Where;

      var baseColumns = baseQuery.Columns.ToList();
      var typeIdColumnIndex = index.Columns
        .Select((c, i) => new {c.Field, i})
        .Single(p => p.Field.IsTypeId && p.Field.IsSystem).i;
      var type = index.ReflectedType;

      var typeMapping = Driver.GetTypeMapping(WellKnownTypes.Int32);

      var binding = new QueryParameterBinding(typeMapping, CreateTypeIdAccessor(TypeIdRegistry[type]).CachingCompile(), QueryParameterBindingType.Regular);

      var typeIdColumn = SqlDml.ColumnRef(
        SqlDml.Column(binding.ParameterReference),
        WellKnown.TypeIdFieldName);
      var discriminatorMap = type.Hierarchy.TypeDiscriminatorMap;
      if (discriminatorMap != null) {
        var discriminatorColumnIndex = underlyingIndex.Columns.IndexOf(discriminatorMap.Column);
        var discriminatorColumn = baseQuery.From.Columns[discriminatorColumnIndex];
        var sqlCase = SqlDml.Case(discriminatorColumn);
        foreach (var pair in discriminatorMap) {
          var discriminatorValue = GetDiscriminatorValue(discriminatorMap, pair.First);
          var typeId = TypeIdRegistry[pair.Second];
          sqlCase.Add(SqlDml.Literal(discriminatorValue), SqlDml.Literal(typeId));
        }
        if (discriminatorMap.Default != null)
          sqlCase.Else = SqlDml.Literal(TypeIdRegistry[discriminatorMap.Default]);
        typeIdColumn = SqlDml.ColumnRef(
          SqlDml.Column(sqlCase),
          WellKnown.TypeIdFieldName);
      }
      baseColumns.Insert(typeIdColumnIndex, typeIdColumn);
      query.Columns.AddRange(baseColumns);

      baseQueryAndBindings.Query = query;
      baseQueryAndBindings.Bindings = new QueryParameterBinding[] { binding }.Union(baseBindings);
      return baseQueryAndBindings;
    }

    private object GetDiscriminatorValue(TypeDiscriminatorMap discriminatorMap, object fieldValue)
    {
      var field = discriminatorMap.Field;
      var column = discriminatorMap.Column;
      return field.ValueType!=column.ValueType
        ? Convert.ChangeType(fieldValue, column.ValueType)
        : fieldValue;
    }

    private Expression<Func<ParameterContext, object>> CreateTypeIdAccessor(int value)
    {
      var bodyExpression = Expression.Convert(Expression.Constant(value),WellKnownTypes.Object);
      var lambdaParemeter = Expression.Parameter(WellKnownOrmTypes.ParameterContext, "context");
      return (Expression<Func<ParameterContext, object>>) FastExpression.Lambda(bodyExpression, lambdaParemeter);
    }
  }
}