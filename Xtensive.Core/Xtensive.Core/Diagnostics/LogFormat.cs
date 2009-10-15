// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Yakunin
// Created:    2009.10.14

namespace Xtensive.Core.Diagnostics
{
  /// <summary>
  /// Log format.
  /// </summary>
  public enum LogFormat
  {
    /// <summary>
    /// Default log format.
    /// The same as <see cref="Comprehensive"/>.
    /// </summary>
    Default = Comprehensive,

    /// <summary>
    /// Comprehensive log format.
    /// </summary>
    Comprehensive = 0,

    /// <summary>
    /// Simple log format.
    /// </summary>
    Simple = 1,

    /// <summary>
    /// Custom log format.
    /// </summary>
    Custom = 2,
  }
}