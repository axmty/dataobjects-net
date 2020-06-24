// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2009.08.19

using System;
using System.Threading;
using System.Threading.Tasks;
using Xtensive.Core;
using Xtensive.Orm.Linq;
using Xtensive.Orm.Linq.Materialization;
using Xtensive.Orm.Rse;

namespace Xtensive.Orm.Internals
{
  /// <summary>
  /// Abstract base for a future query and future scalar implementation.
  /// </summary>
  [Serializable]
  public abstract class DelayedQuery
  {
    private readonly ParameterContext parameterContext;
    private readonly Materializer materializer;

    /// <summary>
    /// Gets <see cref="Session"/> this instance is bound to.
    /// </summary>
    public Session Session { get; }

    /// <summary>
    /// Gets <see cref="StateLifetimeToken"/> this instance is bound to.
    /// </summary>
    public StateLifetimeToken LifetimeToken { get; }

    /// <summary>
    /// Gets the task for this future.
    /// </summary>
    public QueryTask Task { get; }

    /// <summary>
    /// Materializes items from underlying record set.
    /// </summary>
    /// <typeparam name="T">The type of items in the resulting sequence.</typeparam>
    /// <returns><see cref="QueryResult{TItem}"/> representing a sequence to be enumerated.</returns>
    /// <exception cref="InvalidOperationException">Thrown on attempt to get delayed result outside of transaction
    /// boundaries.</exception>
    protected QueryResult<T> Materialize<T>()
    {
      if (!LifetimeToken.IsActive) {
        throw new InvalidOperationException(Strings.ExThisInstanceIsExpiredDueToTransactionBoundaries);
      }

      if (Task.Result==null) {
        Session.ExecuteUserDefinedDelayedQueries(false);
      }

      return materializer.Invoke<T>(RecordSetReader.Create(Task.Result), Session, parameterContext);
    }

    /// <summary>
    /// Materializes items from underlying record set.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <typeparam name="T">The type of items in the resulting sequence.</typeparam>
    /// <returns><see cref="QueryResult{TItem}"/> representing a sequence to be enumerated.</returns>
    /// <exception cref="InvalidOperationException">Thrown on attempt to get delayed result outside of transaction
    /// boundaries.</exception>
    protected async ValueTask<QueryResult<T>> MaterializeAsync<T>(CancellationToken token)
    {
      if (!LifetimeToken.IsActive) {
        throw new InvalidOperationException(Strings.ExThisInstanceIsExpiredDueToTransactionBoundaries);
      }

      if (Task.Result==null) {
        await Session.ExecuteDelayedUserQueriesAsync(false, token);
      }

      return materializer.Invoke<T>(RecordSetReader.Create(Task.Result), Session, parameterContext);
    }

    // Constructors

    /// <summary>
    /// Initializes a new instance of this class.
    /// </summary>
    /// <param name="session"></param>
    /// <param name="translatedQuery">The translated query.</param>
    /// <param name="outerParameterContext">The parameter context.</param>
    internal DelayedQuery(Session session, TranslatedQuery translatedQuery, ParameterContext outerParameterContext)
    {
      ArgumentValidator.EnsureArgumentNotNull(session, nameof(session));
      ArgumentValidator.EnsureArgumentNotNull(translatedQuery, nameof(translatedQuery));
      ArgumentValidator.EnsureArgumentNotNull(parameterContext, nameof(parameterContext));

      Session = session;
      LifetimeToken = session.GetLifetimeToken();

      materializer = translatedQuery.Materializer;
      parameterContext = new ParameterContext(outerParameterContext);
      foreach (var (parameter, tuple) in translatedQuery.TupleParameterBindings) {
        parameterContext.SetValue(parameter, tuple);
      }

      Task = new QueryTask(translatedQuery.DataSource, LifetimeToken, parameterContext);
    }
  }
}