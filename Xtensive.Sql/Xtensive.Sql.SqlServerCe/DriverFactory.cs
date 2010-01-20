// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Krjuchkov
// Created:    2009.06.23

using System;
using System.Data.SqlServerCe;
using Xtensive.Core;
using Xtensive.Sql.Info;
using Xtensive.Sql.SqlServerCe.Resources;

namespace Xtensive.Sql.SqlServerCe
{
  /// <summary>
  /// A <see cref="SqlDriver"/> factory for Microsoft SQL Server.
  /// </summary>
  public class DriverFactory : SqlDriverFactory
  {
    private const string DatabaseAndSchemaQuery =
      "select db_name(), default_schema_name from sys.database_principals where name=user";

    /// <inheritdoc/>
    public override SqlDriver CreateDriver(ConnectionInfo connectionInfo)
    {
      var connectionString = ConnectionStringBuilder.Build(connectionInfo);
      var version = new Version(3, 5, 1, 0);
      var coreServerInfo = new CoreServerInfo {ConnectionString = connectionString, ServerVersion = version};
      return new v3_5.Driver(coreServerInfo);
//      using (var connection = new SqlCeConnection(connectionString)) {
//        connection.Open();
//        var version = new Version(connection.ServerVersion);
//        var coreServerInfo = new CoreServerInfo {ConnectionString = connectionString, ServerVersion = version};
//        SqlHelper.ReadDatabaseAndSchema(connection, DatabaseAndSchemaQuery, coreServerInfo);
//        SqlDriver result;
//        switch (version.Major) {
//        case 9:
//          result = new v3_5.Driver(coreServerInfo);
//          break;
//        case 10:
//          using (var command = connection.CreateCommand()) {
//            command.CommandText = "SELECT @@VERSION";
//            result = ((string) command.ExecuteScalar()).Contains("Azure")
//              ? new Azure.Driver(coreServerInfo)
//              : new v10.Driver(coreServerInfo);
//          }
//          break;
//        default:
//          throw new NotSupportedException(Strings.ExSqlServerBelow2005IsNotSupported);
//        }
//        connection.Close();
//        return result;
//      }
    }
  }
}