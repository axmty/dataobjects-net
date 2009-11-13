// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Krjuchkov
// Created:    2009.11.13

using System;
using System.Collections.Generic;
using System.Linq;
using Xtensive.Sql;
using Xtensive.Sql.Dml;
using Xtensive.Storage.Model;
using Xtensive.Storage.Providers.Sql.Resources;
using Xtensive.Storage.Rse.Providers.Compilable;

namespace Xtensive.Storage.Providers.Sql
{
  partial class SqlCompiler 
  {
    /// <inheritdoc/>
    protected override SqlProvider VisitIndex(IndexProvider provider)
    {
      var index = provider.Index.Resolve(Handlers.Domain.Model);
      SqlSelect query = BuildProviderQuery(index);
      return CreateProvider(query, provider);
    }

    private SqlSelect BuildProviderQuery(IndexInfo index)
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
        throw new NotSupportedException(String.Format(Strings.ExUnsupportedIndex, index.Name, index.Attributes));
      }
      return BuildTableQuery(index);
    }

    private SqlSelect BuildTableQuery(IndexInfo index)
    {
      var domainHandler = (DomainHandler) Handlers.DomainHandler;
      var table = domainHandler.Schema.Tables[index.ReflectedType.MappingName];
      var atRootPolicy = false;
      if (table==null) {
        table = domainHandler.Schema.Tables[index.ReflectedType.GetRoot().MappingName];
        atRootPolicy = true;
      }

      SqlSelect query;
      if (!atRootPolicy) {
        var tableRef = SqlDml.TableRef(table, index.Columns.Select(c => c.Name));
        query = SqlDml.Select(tableRef);
        query.Columns.AddRange(tableRef.Columns.Cast<SqlColumn>());
      }
      else {
        var root = index.ReflectedType.GetRoot().AffectedIndexes.First(i => i.IsPrimary);
        var lookup = root.Columns.ToDictionary(c => c.Field, c => c.Name);
        var tableRef = SqlDml.TableRef(table, index.Columns.Select(c => lookup[c.Field]));
        query = SqlDml.Select(tableRef);
        query.Columns.AddRange(tableRef.Columns.Cast<SqlColumn>());
      }
      return query;
    }

    private SqlSelect BuildUnionQuery(IndexInfo index)
    {
      ISqlQueryExpression result = null;

      var baseQueries = index.UnderlyingIndexes.Select(i => BuildProviderQuery(i)).ToList();
      foreach (var select in baseQueries) {
        result = result==null
          ? (ISqlQueryExpression) select
          : result.Union(select);
      }

      var unionRef = SqlDml.QueryRef(result);
      var query = SqlDml.Select(unionRef);
      query.Columns.AddRange(unionRef.Columns.Cast<SqlColumn>());
      return query;
    }

    private SqlSelect BuildJoinQuery(IndexInfo index)
    {
      SqlTable result = null;
      SqlTable rootTable = null;
      int keyColumnCount = index.KeyColumns.Count;
      var baseQueries = index.UnderlyingIndexes
        .Select(i => BuildProviderQuery(i))
        .ToList();
      var queryRefs = new List<SqlTable>();
      for (int j = 0; j < baseQueries.Count; j++) {
        var baseQuery = baseQueries[j];
        if (result == null) {
          result = SqlDml.QueryRef(baseQuery);
          rootTable = result;
          queryRefs.Add(result);
        }
        else {
          var queryRef = SqlDml.QueryRef(baseQuery);
          queryRefs.Add(queryRef);
          SqlExpression joinExpression = null;
          for (int i = 0; i < keyColumnCount; i++) {
            var binary = (queryRef.Columns[i] == rootTable.Columns[i]);
            if (joinExpression.IsNullReference())
              joinExpression = binary;
            else
              joinExpression &= binary;
          }
          result = result.InnerJoin(queryRef, joinExpression);
        }
      }
      var columns = new List<SqlColumn>();
      foreach (var map in index.ValueColumnsMap) {
        var queryRef = queryRefs[map.First];
        if (columns.Count == 0)
          columns.AddRange(Enumerable.Range(0, keyColumnCount)
            .Select(i => queryRef.Columns[i])
            .Cast<SqlColumn>());
        foreach (var columnIndex in map.Second)
          columns.Add(queryRef.Columns[columnIndex + keyColumnCount]);
      }

      var query = SqlDml.Select(result);
      query.Columns.AddRange(columns);

      return query;
    }

    private SqlSelect BuildFilteredQuery(IndexInfo index)
    {
      var typeIds = index.FilterByTypes.Select(t => t.TypeId).ToArray();
      var underlyingIndex = index.UnderlyingIndexes[0];
      var baseQuery = BuildProviderQuery(underlyingIndex);
      var typeIdColumn = baseQuery.Columns[Handlers.Domain.NameBuilder.TypeIdColumnName];
      var inQuery = SqlDml.In(typeIdColumn, SqlDml.Array(typeIds));
      var query = SqlDml.Select(baseQuery.From);
      query.Columns.AddRange(baseQuery.Columns);
      query.Where = inQuery;
      return query;
    }

    private SqlSelect BuildViewQuery(IndexInfo index)
    {
      var underlyingIndex = index.UnderlyingIndexes[0];
      var baseQuery = BuildProviderQuery(underlyingIndex);
      var query = SqlDml.Select(baseQuery.From);
      query.Where = baseQuery.Where;
      query.Columns.AddRange(index.SelectColumns.Select(i => baseQuery.Columns[i]));
      return query;
    }

  }
}