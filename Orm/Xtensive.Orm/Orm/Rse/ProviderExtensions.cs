// Copyright (C) 2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexis Kochetov
// Created:    2010.09.13

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xtensive.Core;
using Xtensive.Orm.Rse.Providers;

namespace Xtensive.Orm.Rse
{
  /// <summary>
  /// <see cref="CompilableProvider"/> and <see cref="ExecutableProvider"/> related extension methods.
  /// </summary>
  public static class ProviderExtensions
  {
    /// <summary>
    /// Compiles specified <paramref name="provider"/>
    /// and returns new <see cref="RecordSet"/> bound to specified <paramref name="session"/>.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="session">The session.</param>
    /// <param name="parameterContext"><see cref="ParameterContext"/> instance with
    /// the values of query parameters.</param>
    /// <returns>New <see cref="RecordSet"/> bound to specified <paramref name="session"/>.</returns>
    public static RecordSet GetRecordSet(
      this CompilableProvider provider, Session session, ParameterContext parameterContext)
    {
      ArgumentValidator.EnsureArgumentNotNull(provider, nameof(provider));
      ArgumentValidator.EnsureArgumentNotNull(session, nameof(session));
      var enumerationContext = session.CreateEnumerationContext(parameterContext);
      return RecordSet.Create(enumerationContext, session.Compile(provider));
    }

    /// <summary>
    /// Gets <see cref="RecordSet"/> bound to the specified <paramref name="provider"/>.
    /// </summary>
    /// <param name="provider">Provider to get <see cref="RecordSet"/> for.</param>
    /// <param name="session">Session to bind.</param>
    /// <param name="parameterContext"><see cref="ParameterContext"/> instance with
    /// the values of query parameters.</param>
    /// <returns>New <see cref="RecordSet"/> bound to specified <paramref name="session"/>.</returns>
    public static RecordSet GetRecordSet(
      this ExecutableProvider provider, Session session, ParameterContext parameterContext)
    {
      ArgumentValidator.EnsureArgumentNotNull(provider, nameof(provider));
      ArgumentValidator.EnsureArgumentNotNull(session, nameof(session));
      var enumerationContext = session.CreateEnumerationContext(parameterContext);
      return RecordSet.Create(enumerationContext, provider);
    }

    /// <summary>
    /// Asynchronously gets <see cref="RecordSet"/> bound to the specified <paramref name="provider"/>.
    /// </summary>
    /// <param name="provider">Provider to get <see cref="RecordSet"/> for.</param>
    /// <param name="session">Session to bind.</param>
    /// <param name="parameterContext"><see cref="ParameterContext"/> instance with
    /// the values of query parameters.</param>
    /// <param name="token">Token to cancel operation.</param>
    /// <returns>Task performing this operation.</returns>
    public static async Task<RecordSet> GetRecordSetAsync(
      this ExecutableProvider provider, Session session, ParameterContext parameterContext, CancellationToken token)
    {
      ArgumentValidator.EnsureArgumentNotNull(provider, nameof(provider));
      ArgumentValidator.EnsureArgumentNotNull(session, nameof(session));
      var enumerationContext =
        await session.CreateEnumerationContextAsync(parameterContext, token).ConfigureAwait(false);
      return await RecordSet.CreateAsync(enumerationContext, provider);
    }

    /// <summary>
    /// Calculates count of elements of provided <paramref name="provider"/>.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="session">The session.</param>
    public static long Count(this CompilableProvider provider, Session session)
    {
      ArgumentValidator.EnsureArgumentNotNull(provider, nameof(provider));
      ArgumentValidator.EnsureArgumentNotNull(session, nameof(session));
      return provider
        .Aggregate(null, new AggregateColumnDescriptor("$Count", 0, AggregateType.Count))
        .GetRecordSet(session, new ParameterContext())
        .First()
        .GetValue<long>(0);
    }
  }
}