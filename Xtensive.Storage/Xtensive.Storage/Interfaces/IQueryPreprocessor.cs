// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Gamzov
// Created:    2009.12.15

using System.Linq.Expressions;

namespace Xtensive.Storage
{
  /// <summary>
  /// An interface that must be implemented by any
  /// LINQ query preprocessor.
  /// </summary>
  public interface IQueryPreprocessor
  {
    /// <summary>
    /// Applies the preprocessor to the specified query.
    /// </summary>
    /// <param name="query">The query to apply the preprocessor to.</param>
    /// <returns>Application (preprocessing) result.</returns>
    Expression Apply(Expression query);

    /// <summary>
    /// Determines whether this query preprocessor is dependent on <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The other query preprocessor.</param>
    /// <returns>
    /// <see langword="true"/> if this query preprocessor 
    /// is dependent on <paramref name="other"/>; 
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool IsDependentOn(IQueryPreprocessor other);
  }
}