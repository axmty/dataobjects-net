// Copyright (C) 2012-2022 Xtensive LLC.
// This code is distributed under MIT license terms.
// See the License.txt file in the project root for more information.
// Created by: Denis Krjuchkov
// Created:    2012.03.07

using System;
using Xtensive.Core;
using Xtensive.Orm.Configuration;
using Xtensive.Sql;
using Xtensive.Sql.Compiler;
using Xtensive.Sql.Dml;
using Xtensive.Sql.Model;

namespace Xtensive.Orm.Providers
{
  internal sealed class SequenceQueryBuilder
  {
    private readonly StorageDriver driver;
    private readonly bool hasSequences;
    private readonly bool hasBatches;
    private readonly bool hasInsertDefaultValues;
    private readonly bool storesAutoIncrementSettingsInMemory;
    private readonly SequenceQueryCompartment compartment;

    public SequenceQuery BuildNextValueQuery(SchemaNode generatorNode, NodeConfiguration nodeConfiguration, long increment, bool forcedSameSessionExecution)
    {
      var actualCompartment = forcedSameSessionExecution
        ? SequenceQueryCompartment.SameSession
        : compartment;

      var postCompilerConfiguration = (nodeConfiguration != null)
        ? new SqlPostCompilerConfiguration(nodeConfiguration.GetDatabaseMapping(), nodeConfiguration.GetSchemaMapping())
        : new SqlPostCompilerConfiguration();

      var sqlNext = hasSequences
        ? GetSequenceBasedNextImplementation(generatorNode, increment)
        : GetTableBasedNextImplementation(generatorNode);

      var requiresSeparateSession = !hasSequences;
      var batch = sqlNext as SqlBatch;
      if (batch == null || hasBatches)
        // There are batches or there is single statement, so we can run this as a single request
        return new SequenceQuery(Compile(sqlNext, nodeConfiguration).GetCommandText(postCompilerConfiguration), actualCompartment);

      // No batches, so we must execute this manually
      if (!storesAutoIncrementSettingsInMemory)
        return new SequenceQuery(
          Compile((ISqlCompileUnit)batch[0], nodeConfiguration).GetCommandText(postCompilerConfiguration),
          Compile((ISqlCompileUnit)batch[1], nodeConfiguration).GetCommandText(postCompilerConfiguration),
          actualCompartment);
      return new SequenceQuery(
          Compile((ISqlCompileUnit)batch[0], nodeConfiguration).GetCommandText(postCompilerConfiguration),
          Compile((ISqlCompileUnit)batch[1], nodeConfiguration).GetCommandText(postCompilerConfiguration),
          Compile((ISqlCompileUnit)batch[2], nodeConfiguration).GetCommandText(postCompilerConfiguration),
          actualCompartment);
    }

    public SequenceQuery BuildNextValueQuery(SchemaNode generatorNode, NodeConfiguration nodeConfiguration, long increment)
    {
      return BuildNextValueQuery(generatorNode, nodeConfiguration, increment, false);
    }

    public SequenceQuery BuildNextValueQuery(SchemaNode generatorNode, long increment, bool forcedSameSessionExecution)
    {
      return BuildNextValueQuery(generatorNode, null, increment, forcedSameSessionExecution);
    }

    public SequenceQuery BuildNextValueQuery(SchemaNode generatorNode, long increment)
    {
      return BuildNextValueQuery(generatorNode, null, increment);
    }

    public string BuildCleanUpQuery(SchemaNode generatorNode)
    {
      return BuildCleanUpQuery(generatorNode, null);
    }

    public string BuildCleanUpQuery(SchemaNode generatorNode, NodeConfiguration nodeConfiguration)
    {
      var postCompilerConfiguration = (nodeConfiguration != null)
        ? new SqlPostCompilerConfiguration(nodeConfiguration.GetDatabaseMapping(), nodeConfiguration.GetSchemaMapping())
        : new SqlPostCompilerConfiguration();

      var table = (Table)generatorNode;
      var delete = SqlDml.Delete(SqlDml.TableRef(table));
      return Compile(delete, nodeConfiguration).GetCommandText(postCompilerConfiguration);
    }


    private SqlCompilationResult Compile(ISqlCompileUnit unit, NodeConfiguration nodeConfiguration)
    {
      if (nodeConfiguration!=null)
        return driver.Compile(unit, nodeConfiguration);
      return driver.Compile(unit);
    }

    private ISqlCompileUnit GetSequenceBasedNextImplementation(SchemaNode generatorNode, long increment)
    {
      return SqlDml.Select(SqlDml.NextValue((Sequence) generatorNode, (int) increment));
    }

    private ISqlCompileUnit GetTableBasedNextImplementation(SchemaNode generatorNode)
    {
      var table = (Table) generatorNode;

      var idColumn = GetColumn(table, WellKnown.GeneratorColumnName);

      var tableRef = SqlDml.TableRef(table);
      var insert = SqlDml.Insert(tableRef);
      var delete = SqlDml.Delete(tableRef);

      if (!hasInsertDefaultValues) {
        var fakeColumn = GetColumn(table, WellKnown.GeneratorFakeColumnName);
        insert.Values[tableRef[fakeColumn.Name]] = SqlDml.Null;
      }

      var result = SqlDml.Batch();
      if (storesAutoIncrementSettingsInMemory)
        result.Add(delete);
      result.Add(insert);
      result.Add(SqlDml.Select(SqlDml.LastAutoGeneratedId()));
      return result;
    }

    private static TableColumn GetColumn(Table table, string columnName)
    {
      var idColumn = table.TableColumns[columnName];
      if (idColumn==null)
        throw new InvalidOperationException(string.Format(
          Strings.ExColumnXIsNotFoundInTableY, columnName, table.Name));
      return idColumn;
    }

    public SequenceQueryBuilder(StorageDriver driver)
    {
      ArgumentValidator.EnsureArgumentNotNull(driver, "driver");

      this.driver = driver;

      var providerInfo = driver.ProviderInfo;

      hasSequences = providerInfo.Supports(ProviderFeatures.Sequences);
      hasBatches = providerInfo.Supports(ProviderFeatures.DmlBatches);
      hasInsertDefaultValues = providerInfo.Supports(ProviderFeatures.InsertDefaultValues);
      storesAutoIncrementSettingsInMemory = providerInfo.Supports(ProviderFeatures.AutoIncrementSettingsInMemory);

      compartment = hasSequences || providerInfo.Supports(ProviderFeatures.TransactionalKeyGenerators)
        ? SequenceQueryCompartment.SameSession
        : SequenceQueryCompartment.SeparateSession;
    }
  }
}