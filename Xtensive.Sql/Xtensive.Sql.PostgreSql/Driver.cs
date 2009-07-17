using System.Data.Common;
using Xtensive.Core;
using Xtensive.Sql.Info;

namespace Xtensive.Sql.PostgreSql
{
  internal abstract class Driver : SqlDriver
  {
    protected override DbConnection CreateNativeConnection(UrlInfo url)
    {
      return ConnectionFactory.CreateConnection(url);
    }

    protected override ValueTypeMapping.DataAccessHandler CreateDataAccessHandler()
    {
      return new DataAccessHandler(this);
    }

    // Constructors

    protected Driver(ServerInfoProvider serverInfoProvider)
      : base(serverInfoProvider)
    {
    }
  }
}