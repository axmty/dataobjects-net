// Copyright (C) 2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Yakunin
// Created:    2010.02.09

using System;
using System.Data.Common;

namespace Xtensive.Orm.Providers
{
  public partial class SqlSessionHandler
  {
    // Implementation of IDirectSqlService

    /// <inheritdoc/>
    DbConnection IDirectSqlService.Connection {
      get {
        EnsureConnectionIsOpen();
        return connection.UnderlyingConnection;
      }
    }

    /// <inheritdoc/>
    DbTransaction IDirectSqlService.Transaction {
      get {
        EnsureConnectionIsOpen();
        return connection.ActiveTransaction;
      }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Connection is not open.</exception>
    DbCommand IDirectSqlService.CreateCommand()
    {
      EnsureConnectionIsOpen();
      return connection.CreateCommand();
    }
  }
}