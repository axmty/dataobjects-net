// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Krjuchkov
// Created:    2009.07.07

using System;
using Xtensive.Sql.Compiler;
using SqlServerConnection = System.Data.SqlClient.SqlConnection;

namespace Xtensive.Sql.SqlServer.v10
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

    protected Driver(ServerInfoProvider serverInfoProvider)
      : base(serverInfoProvider)
    {}
  }
}