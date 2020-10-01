﻿// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alena Mikshina
// Created:    2014.04.09

namespace Xtensive.Sql.Drivers.PostgreSql.v8_0
{
  public static class CustomSqlType
  {
    /// <summary>
    /// Point, like in NpgsqlTypes
    /// </summary>
    public static readonly SqlType Point = new SqlType("Point");

    /// <summary>
    /// LSeg, like in NpgsqlTypes
    /// </summary>
    public static readonly SqlType LSeg = new SqlType("LSeg");

    /// <summary>
    /// Box, like in NpgsqlTypes
    /// </summary>
    public static readonly SqlType Box = new SqlType("Box");

    /// <summary>
    /// Path, like in NpgsqlTypes
    /// </summary>
    public static readonly SqlType Path = new SqlType("Path");

    /// <summary>
    /// Polygon, like in NpgsqlTypes
    /// </summary>
    public static readonly SqlType Polygon = new SqlType("Polygon");

    /// <summary>
    /// Circle, like in NpgsqlTypes
    /// </summary>
    public static readonly SqlType Circle = new SqlType("Circle");
  }
}
