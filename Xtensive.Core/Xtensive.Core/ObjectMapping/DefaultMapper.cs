// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2009.12.10

namespace Xtensive.Core.ObjectMapping
{
  /// <summary>
  /// Default concrete heir of <see cref="MapperBase"/>.
  /// </summary>
  public class DefaultMapper : MapperBase
  {
    private DefaultOperationSet operationSet;

    /// <inheritdoc/>
    protected override void OnObjectModified(OperationInfo descriptor)
    {
      operationSet.Add(descriptor);
    }

    /// <inheritdoc/>
    protected override void InitializeComparison(object originalTarget, object modifiedTarget)
    {
      operationSet = new DefaultOperationSet();
    }

    /// <inheritdoc/>
    protected override IOperationSet GetComparisonResult(object originalTarget, object modifiedTarget)
    {
      operationSet.Lock();
      return operationSet;
    }
  }
}