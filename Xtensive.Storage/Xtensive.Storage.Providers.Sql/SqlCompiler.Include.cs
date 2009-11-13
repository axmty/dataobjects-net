// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Krjuchkov
// Created:    2009.11.13

using System;
using System.Collections.Generic;
using System.Linq;
using Xtensive.Core.Collections;
using Xtensive.Core.Linq;
using Xtensive.Core.Tuples;
using Xtensive.Sql;
using Xtensive.Sql.Dml;
using Xtensive.Storage.Rse;
using Xtensive.Storage.Rse.Providers.Compilable;

namespace Xtensive.Storage.Providers.Sql
{
  partial class SqlCompiler 
  {
    /// <inheritdoc/>
    protected override SqlProvider VisitInclude(IncludeProvider provider)
    {
      var source = Compile(provider.Source);
      var resultQuery = ExtractSqlSelect(provider, source);
      var sourceColumns = ExtractColumnExpressions(resultQuery, provider);
      var bindings = source.Request.ParameterBindings;
      var filterDataSource = provider.FilterDataSource.CachingCompile();
      var requestOptions = RequestOptions.Empty;
      SqlExpression resultExpression;
      TemporaryTableDescriptor tableDescriptor = null;
      QueryParameterBinding extraBinding = null;

      switch (provider.Algorithm) {
      case IncludeAlgorithm.Auto:
        var complexConditionExpression = CreateIncludeViaComplexConditionExpression(
          provider, BuildRowFilterParameterAccessor(filterDataSource, true),
          sourceColumns, out extraBinding);
        var temporaryTableExpression = CreateIncludeViaTemporaryTableExpression(
          provider, sourceColumns, out tableDescriptor);
        resultExpression = SqlDml.Variant(extraBinding,
          complexConditionExpression, temporaryTableExpression);
        break;
      case IncludeAlgorithm.ComplexCondition:
        resultExpression = CreateIncludeViaComplexConditionExpression(
          provider, BuildRowFilterParameterAccessor(filterDataSource, false),
          sourceColumns, out extraBinding);
        requestOptions |= RequestOptions.AllowBatching;
        break;
      case IncludeAlgorithm.TemporaryTable:
        resultExpression = CreateIncludeViaTemporaryTableExpression(
          provider, sourceColumns, out tableDescriptor);
        break;
      default:
        throw new ArgumentOutOfRangeException("provider.Algorithm");
      }

      if (!ProviderInfo.Supports(ProviderFeatures.FullFledgedBooleanExpressions))
        resultExpression = booleanExpressionConverter.BooleanToInt(resultExpression);
      resultQuery.Columns.Add(resultExpression, provider.ResultColumnName);
      if (extraBinding!=null)
        bindings = bindings.Concat(EnumerableUtils.One(extraBinding));
      var request = new QueryRequest(resultQuery, provider.Header.TupleDescriptor, requestOptions, bindings);
      return new SqlIncludeProvider(Handlers, request, tableDescriptor, filterDataSource, provider, source);
    }

    private SqlExpression CreateIncludeViaComplexConditionExpression(
      IncludeProvider provider, Func<object> valueAccessor,
      IList<SqlExpression> sourceColumns, out QueryParameterBinding binding)
    {
      var filterTupleDescriptor = provider.FilteredColumnsExtractionTransform.Descriptor;
      var mappings = filterTupleDescriptor.Select(type => Driver.GetTypeMapping(type));
      binding = new QueryRowFilterParameterBinding(valueAccessor, mappings);
      var resultExpression = SqlDml.DynamicFilter(binding);
      resultExpression.Expressions.AddRange(provider.FilteredColumns.Select(index => sourceColumns[index]));
      return resultExpression;
    }

    private SqlExpression CreateIncludeViaTemporaryTableExpression(
      IncludeProvider provider, IList<SqlExpression> sourceColumns,
      out TemporaryTableDescriptor tableDescriptor)
    {
      var filterTupleDescriptor = provider.FilteredColumnsExtractionTransform.Descriptor;
      tableDescriptor = DomainHandler.TemporaryTableManager
        .BuildDescriptor(Guid.NewGuid().ToString(), filterTupleDescriptor);
      var filterQuery = tableDescriptor.QueryStatement.ShallowClone();
      var tableRef = filterQuery.From;
      for (int i = 0; i < filterTupleDescriptor.Count; i++)
        filterQuery.Where &= sourceColumns[i]==tableRef[i];
      var resultExpression = SqlDml.Exists(filterQuery);
      return resultExpression;
    }

    private static Func<object> BuildRowFilterParameterAccessor(
      Func<IEnumerable<Tuple>> filterDataSource, bool takeFromContext)
    {
      if (!takeFromContext)
        return () => filterDataSource.Invoke().ToList();

      return () => EnumerationContext.Current
        .GetValue<List<Tuple>>(filterDataSource, SqlIncludeProvider.RowFilterDataName);
    }
  }
}