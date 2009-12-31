// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Gamzov
// Created:    2009.12.28

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xtensive.Core;
using Xtensive.Storage.FullText;
using Xtensive.Storage.Model;

namespace Xtensive.Storage.Linq.Expressions
{
  [Serializable]
  internal class FreeTextExpression : ParameterizedExpression,
    IMappedExpression
  {
    public FullTextIndexInfo FullTextIndex { get; private set; }

    public ColumnExpression RankExpression { get; private set; }

    public EntityExpression EntityExpression { get; private set; }

    /// <exception cref="NotSupportedException"><c>NotSupportedException</c>.</exception>
    public Segment<int> Mapping
    {
      get { throw new NotSupportedException(); }
    }

    public Expression BindParameter(ParameterExpression parameter, Dictionary<Expression, Expression> processedExpressions)
    {
      Expression result;
      if (processedExpressions.TryGetValue(this, out result))
        return result;

      var entityExpression = (EntityExpression) EntityExpression.BindParameter(parameter, processedExpressions);
      var rankExpression = (ColumnExpression) RankExpression.BindParameter(parameter, processedExpressions);
      return new FreeTextExpression(FullTextIndex, entityExpression, rankExpression, parameter);
    }

    public Expression RemoveOuterParameter(Dictionary<Expression, Expression> processedExpressions)
    {
      Expression result;
      if (processedExpressions.TryGetValue(this, out result))
        return result;

      var entityExpression = (EntityExpression) EntityExpression.RemoveOuterParameter(processedExpressions);
      var rankExpression = (ColumnExpression) RankExpression.RemoveOuterParameter(processedExpressions);
      return new FreeTextExpression(FullTextIndex, entityExpression, rankExpression, null);
    }

    public Expression Remap(int offset, Dictionary<Expression, Expression> processedExpressions)
    {
      if (!CanRemap)
        return this;

      Expression result;
      if (processedExpressions.TryGetValue(this, out result))
        return result;

      var remappedEntityExpression = (EntityExpression) EntityExpression.Remap(offset, processedExpressions);
      var remappedRankExpression = (ColumnExpression) RankExpression.Remap(offset, processedExpressions);
      return new FreeTextExpression(FullTextIndex, remappedEntityExpression, remappedRankExpression, OuterParameter);
    }

    public Expression Remap(int[] map, Dictionary<Expression, Expression> processedExpressions)
    {
      if (!CanRemap)
        return this;

      Expression result;
      if (processedExpressions.TryGetValue(this, out result))
        return result;

      var remappedEntityExpression = (EntityExpression) EntityExpression.Remap(map, processedExpressions);
      var remappedRankExpression = (ColumnExpression) RankExpression.Remap(map, processedExpressions);
      return new FreeTextExpression(FullTextIndex, remappedEntityExpression, remappedRankExpression, OuterParameter);
    }

    public FreeTextExpression(FullTextIndexInfo fullTextIndex, EntityExpression entityExpression, ColumnExpression rankExpression, ParameterExpression parameter)
      : base(ExtendedExpressionType.FreeText, typeof (FullTextMatch<>).MakeGenericType(fullTextIndex.PrimaryIndex.ReflectedType.UnderlyingType), parameter, false)
    {
      FullTextIndex = fullTextIndex;
      RankExpression = rankExpression;
      EntityExpression = entityExpression;
    }
  }
}