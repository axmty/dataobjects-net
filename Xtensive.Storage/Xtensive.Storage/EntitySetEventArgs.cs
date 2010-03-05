// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexis Kochetov
// Created:    2009.10.23

using System;
using System.Diagnostics;
using Xtensive.Core.Internals.DocTemplates;

namespace Xtensive.Storage
{
  /// <summary>
  /// Describes <see cref="EntitySet{TItem}"/>-related events.
  /// </summary>
  public class EntitySetEventArgs : EntityFieldEventArgs
  {
    /// <summary>
    /// Gets the <see cref="EntitySetBase"/> to which this event is related.
    /// </summary>
    public EntitySetBase EntitySet { get; private set; }


    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true" />
    /// </summary>
    /// <param name="entitySet">The entity set.</param>
    public EntitySetEventArgs(EntitySetBase entitySet)
      : base(entitySet.Owner, entitySet.Field)
    {
      EntitySet = entitySet;
    }
  }
}