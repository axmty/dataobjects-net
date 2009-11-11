// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.

using System;
using Xtensive.Sql.Compiler;
using SqlServerConnection = System.Data.SqlClient.SqlConnection;

namespace Xtensive.Sql.SqlServer.v09
{
  internal class Driver : SqlServer.Driver
  {
    protected override SqlCompiler CreateCompiler()
    {
      return new Compiler(this);
    }

    protected override Model.Extractor CreateExtractor()
    {
      return new Extractor(this);
    }

    protected override SqlTranslator CreateTranslator()
    {
      return new Translator(this);
    }

    // Constructors

    public Driver(SqlServerConnection connection, Version version)
      : base(new ServerInfoProvider(connection, version))
    {
    }
  }
}