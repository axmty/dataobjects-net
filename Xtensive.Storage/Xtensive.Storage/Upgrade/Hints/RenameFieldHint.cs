// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Kofman
// Created:    2009.04.29

using System;
using Xtensive.Core;
using Xtensive.Internals.DocTemplates;

namespace Xtensive.Storage.Upgrade
{
  /// <summary>
  /// Rename field hint.
  /// </summary>
  [Serializable]
  public sealed class RenameFieldHint : UpgradeHint,
    IEquatable<RenameFieldHint>
  {
    private const string ToStringFormat = "Rename field: {0} {1} -> {2}";

    /// <summary>
    /// Gets or sets the type of the target.
    /// </summary>
    public Type TargetType { get; private set; }

    /// <summary>
    /// Gets the old field name.
    /// </summary>    
    public string OldFieldName { get; private set; }

    /// <summary>
    /// Gets new field name.
    /// </summary>
    public string NewFieldName { get; private set; }

    /// <inheritdoc/>
    public bool Equals(RenameFieldHint other)
    {
      if (ReferenceEquals(null, other))
        return false;
      if (ReferenceEquals(this, other))
        return true;
      return base.Equals(other) 
        && other.TargetType==TargetType 
        && other.OldFieldName==OldFieldName
        && other.NewFieldName==NewFieldName;
    }

    /// <inheritdoc/>
    public override bool Equals(UpgradeHint other)
    {
      return Equals(other as RenameFieldHint);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
      unchecked {
        int result = base.GetHashCode();
        result = (result * 397) ^ (TargetType!=null ? TargetType.GetHashCode() : 0);
        result = (result * 397) ^ (OldFieldName!=null ? OldFieldName.GetHashCode() : 0);
        result = (result * 397) ^ (NewFieldName!=null ? NewFieldName.GetHashCode() : 0);
        return result;
      }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
      return string.Format(ToStringFormat, TargetType.FullName, OldFieldName, NewFieldName);
    }


    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="targetType">The current type.</param>
    /// <param name="oldFieldName">Old name of the field.</param>
    /// <param name="newFieldName">New name of the field.</param>
    public RenameFieldHint(Type targetType, string oldFieldName, string newFieldName)
    {
      ArgumentValidator.EnsureArgumentNotNull(targetType, "targetType");
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(oldFieldName, "oldFieldName");
      ArgumentValidator.EnsureArgumentNotNull(newFieldName, "newFieldName");

      TargetType = targetType;
      OldFieldName = oldFieldName;
      NewFieldName = newFieldName;
    }
  }
}