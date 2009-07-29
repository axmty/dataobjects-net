// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2007.08.10

using System;
using System.Diagnostics;
using Xtensive.Core;
using Xtensive.Core.Caching;
using Xtensive.Core.Collections;
using Xtensive.Core.Diagnostics;
using Xtensive.Core.Disposing;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Integrity.Atomicity;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Internals;
using Xtensive.Storage.PairIntegrity;
using Xtensive.Storage.Providers;
using Xtensive.Storage.ReferentialIntegrity;
using Xtensive.Storage.Resources;

namespace Xtensive.Storage
{
  /// <summary>
  /// Data context, which all persistent objects are bound to.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Each session has its own connection to database and set of materialized persistent instates.
  /// It contains identity map and tracks changes in bound persistent classes.
  /// </para>
  /// <para>
  /// <c>Session</c> implements <see cref="IContext"/> interface, it means that each <c>Session</c>
  /// can be either active or not active in particular thread (see <see cref="IsActive"/> property).
  /// Each thread can contain only one active session, it can be a accessed via 
  /// <see cref="Current">Session.Current</see> property or <see cref="Demand">Session.Demand()</see> method.
  /// </para>
  /// <para>
  /// Session can be open and activated by <see cref="Domain.OpenSession()">Domain.OpenSession()</see> method. 
  /// Existing session can be activated by <see cref="Activate"/> method.
  /// </para>
  /// </remarks>
  /// <example>
  /// <code source="..\..\Xtensive.Storage\Xtensive.Storage.Manual\DomainAndSessionSample.cs" region="Session sample"></code>
  /// </example>
  /// <seealso cref="Domain"/>
  /// <seealso cref="SessionBound" />
  public sealed partial class Session : DomainBound,
    IContext<SessionScope>, IResource
  {
    private const int EntityStateRegistrySizeLimit = 250; // TODO: -> SessionConfiguration
    private readonly bool persistRequiresTopologicalSort;
    private bool isPersisting;
    private volatile bool isDisposed;
    private readonly Set<object> consumers = new Set<object>();
    private readonly object _lock = new object();
    private ServiceProvider serviceProvider;

    /// <summary>
    /// Gets the configuration of the <see cref="Session"/>.
    /// </summary>
    public SessionConfiguration Configuration { get; private set; }

    /// <summary>
    /// Gets the name of the <see cref="Session"/>
    /// (useful mainly for debugging purposes - e.g. it is used in logs).
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Indicates whether debug event logging is enabled.
    /// Caches <see cref="Log.IsLogged"/> method result for <see cref="LogEventTypes.Debug"/> event.
    /// </summary>
    public bool IsDebugEventLoggingEnabled { get; private set; }

    #region Private \ internal members

    internal SessionHandler Handler { get; set; }

    internal HandlerAccessor Handlers { get; private set; }

    internal CoreServiceAccessor CoreServices { get; private set; }

    internal SyncManager PairSyncManager { get; private set; }

    internal RemovalProcessor RemovalProcessor { get; private set; }

    internal CompilationContext CompilationContext
    {
      get { return Handlers.DomainHandler.CompilationContext; }
    }

    private void NotifyDisposing()
    {
      if (!SystemLogicOnly && OnDisposing!=null)
        OnDisposing(this, EventArgs.Empty);
    }

    private void NotifyPersisting()
    {
      if (!SystemLogicOnly && OnPersisting!=null)
        OnPersisting(this, EventArgs.Empty);
    }

    private void NotifyPersist()
    {
      if (!SystemLogicOnly && OnPersist!=null)
        OnPersist(this, EventArgs.Empty);
    }

    internal void NotifyCreateEntity(Entity entity)
    {
      if (!SystemLogicOnly && OnCreateEntity!=null)
        OnCreateEntity(this, new EntityEventArgs(entity));
    }

    internal void NotifyRemovingEntity(Entity entity)
    {
      if (!SystemLogicOnly && OnRemovingEntity!=null)
        OnRemovingEntity(this, new EntityEventArgs(entity));
    }

    internal void NotifyRemoveEntity(Entity entity)
    {
      if (!SystemLogicOnly && OnRemoveEntity!=null)
        OnRemoveEntity(this, new EntityEventArgs(entity));
    }

    #endregion

    /// <summary>
    /// Gets the session service provider.
    /// </summary>
    public ServiceProvider Services {
      get {
        if (serviceProvider==null)
          serviceProvider = new ServiceProvider(this);
        return serviceProvider;
      }
    }

    #region IResource methods

    /// <inheritdoc/>
    void IResource.AddConsumer(object consumer)
    {
      consumers.Add(consumer);
    }

    /// <inheritdoc/>
    void IResource.RemoveConsumer(object consumer)
    {
      consumers.Remove(consumer);
      if (!(this as IResource).HasConsumers)
        Dispose();
    }

    /// <inheritdoc/>
    bool IResource.HasConsumers
    {
      get { return consumers.Count > 0; }
    }

    #endregion

    #region IContext<...> methods

    private static Func<Session> currentSessionResolver;

    /// <summary>
    /// Sets the current session resolver.
    /// </summary>
    /// <remarks>
    /// This method can be called once per application domain, assigned resolver can not be changed.
    /// </remarks>
    /// <param name="resolver">The delegate that resolves current session.</param>
    /// <exception cref="InvalidOperationException">Current session resolver is already assigned.</exception>
    public static void SetCurrentSessionResolver(Func<Session> resolver)
    {
      ArgumentValidator.EnsureArgumentNotNull(resolver, "resolver");
      if (currentSessionResolver!=null)
        throw new InvalidOperationException(Strings.ExValueIsAlreadyAssigned);
      currentSessionResolver = resolver;
      Rse.Compilation.CompilationContext.SetCurrentContextResolver(
        () => {
          var session = resolver.Invoke();
          return session==null ? null : session.CompilationContext;
        });
    }

    /// <summary>
    /// Gets the current active <see cref="Session"/> instance.
    /// </summary>
    public static Session Current
    {
      [DebuggerStepThrough]
      get
      {
        return 
          SessionScope.CurrentSession ?? 
          (currentSessionResolver==null ? null : currentSessionResolver.Invoke());
      }
    }

    /// <summary>
    /// Gets the current <see cref="Session"/>, 
    /// or throws <see cref="InvalidOperationException"/>, 
    /// if active <see cref="Session"/> is not found.
    /// </summary>
    /// <returns>Current session.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Session.Current"/> <see cref="Session"/> is <see langword="null" />.</exception>
    public static Session Demand()
    {
      var currentSession = Current;
      if (currentSession==null)
        throw new InvalidOperationException(Strings.ExActiveSessionIsRequiredForThisOperation);
      return currentSession;
    }

    /// <inheritdoc/>
    public SessionScope Activate()
    {
      if (SessionScope.CurrentSession==this)
        return null;
      return new SessionScope(this);
    }

    /// <inheritdoc/>
    IDisposable IContext.Activate()
    {
      return Activate();
    }

    /// <inheritdoc/>
    public bool IsActive
    {
      get { return Current==this; }
    }

    #endregion

    #region EnsureXxx methods

    /// <summary>
    /// Ensures the object is not disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Object is already disposed.</exception>
    protected void EnsureNotDisposed()
    {
      if (isDisposed)
        throw new ObjectDisposedException(Strings.ExSessionIsAlreadyDisposed);
    }

    #endregion

    #region Open methods

    /// <summary>
    /// Opens and activates new <see cref="Session"/> with default <see cref="SessionConfiguration"/>.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <returns>
    /// New <see cref="SessionConsumptionScope"/> object.
    /// </returns>
    /// <remarks>
    /// Session will be closed when returned <see cref="SessionConsumptionScope"/> is disposed.
    /// </remarks>
    /// <sample><code>
    /// using (Session.Open(domain)) {
    /// // work with persistent objects here
    /// // Session is available through static Session.Current property
    /// }
    /// </code></sample>
    /// <seealso cref="Session"/>
    public static SessionConsumptionScope Open(Domain domain)
    {
      ArgumentValidator.EnsureArgumentNotNull(domain, "domain");
      return Open(domain, domain.Configuration.Sessions.Default, true);
    }

    /// <summary>
    /// Opens new <see cref="Session"/> with default <see cref="SessionConfiguration"/>.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <param name="activate">Determines whether created session should be activated or not.</param>
    /// </param>
    /// <returns>
    /// New <see cref="SessionConsumptionScope"/> object.
    /// </returns>
    /// <remarks>
    /// Session will be closed when returned <see cref="SessionConsumptionScope"/> is disposed.
    /// </remarks>
    /// <sample><code>
    /// using (Session.Open(domain)) {
    /// // work with persistent objects here
    /// // Session is available through static Session.Current property
    /// }
    /// </code></sample>
    /// <seealso cref="Session"/>
    public static SessionConsumptionScope Open(Domain domain, bool activate)
    {
      ArgumentValidator.EnsureArgumentNotNull(domain, "domain");
      return Open(domain, domain.Configuration.Sessions.Default, activate);
    }

    /// <summary>
    /// Opens and activates new <see cref="Session"/> of specified <see cref="SessionType"/>.
    /// </summary>
    /// <param name="domain">The domain.</param>    
    /// <param name="type">The type of session.</param>
    /// </param>
    /// <returns>
    /// New <see cref="SessionConsumptionScope"/> object.
    /// </returns>
    /// <remarks>
    /// Session will be closed when returned <see cref="SessionConsumptionScope"/> is disposed.
    /// </remarks>
    /// <sample><code>
    /// using (Session.Open(domain, sessionType)) {
    /// // work with persistent objects here
    /// // Session is available through static Session.Current property
    /// }
    /// </code></sample>
    public static SessionConsumptionScope Open(Domain domain, SessionType type)
    {
      return Open(domain, type, true);
    }

    /// <summary>
    /// Opens new <see cref="Session"/> of specified <see cref="SessionType"/>.
    /// </summary>
    /// <param name="domain">The domain.</param>    
    /// <param name="type">The type of session.</param>
    /// <param name="activate">Determines whether created session should be activated or not.</param>
    /// </param>
    /// <returns>
    /// New <see cref="SessionConsumptionScope"/> object.
    /// </returns>
    /// <remarks>
    /// Session will be closed when returned <see cref="SessionConsumptionScope"/> is disposed.
    /// </remarks>
    /// <sample><code>
    /// using (Session.Open(domain, sessionType, false)) {
    /// // work with persistent objects here
    /// // Session is available through static Session.Current property
    /// }
    /// </code></sample>
    public static SessionConsumptionScope Open(Domain domain, SessionType type, bool activate)
    {
      ArgumentValidator.EnsureArgumentNotNull(domain, "domain");

      switch (type) {
      case SessionType.User:
        return Open(domain, domain.Configuration.Sessions.Default, activate);
      case SessionType.System:
        return Open(domain, domain.Configuration.Sessions.System, activate);
      case SessionType.KeyGenerator:
        return Open(domain, domain.Configuration.Sessions.KeyGenerator, activate);
      case SessionType.Service:
        return Open(domain, domain.Configuration.Sessions.Service, activate);
      default:
        throw new ArgumentOutOfRangeException("type");
      }
    }

    /// <summary>
    /// Opens and activates new <see cref="Session"/> with specified <see cref="SessionConfiguration"/>.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <param name="configuration">The session configuration.</param>
    /// <returns>
    /// New <see cref="SessionConsumptionScope"/> object.
    /// </returns>
    /// <remarks>
    /// Session will be closed when returned <see cref="SessionConsumptionScope"/> is disposed.
    /// </remarks>
    /// <sample><code>
    /// using (Session.Open(domain, sessionConfiguration)) {
    /// // work with persistent objects here
    /// // Session is available through static Session.Current property
    /// }
    /// </code></sample>
    /// <seealso cref="Session"/>
    public static SessionConsumptionScope Open(Domain domain, SessionConfiguration configuration)
    {
      return Open(domain, configuration, true);
    }

    /// <summary>
    /// Opens new <see cref="Session"/> with specified <see cref="SessionConfiguration"/>.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <param name="configuration">The session configuration.</param>
    /// <param name="activate">Determines whether created session should be activated or not.</param>
    /// <returns>
    /// New <see cref="SessionConsumptionScope"/> object.
    /// </returns>
    /// <remarks>
    /// Session will be closed when returned <see cref="SessionConsumptionScope"/> is disposed.
    /// </remarks>
    /// <sample><code>
    /// using (Session.Open(domain, sessionConfiguration, false)) {
    /// // work with persistent objects here
    /// // Session is available through static Session.Current property
    /// }
    /// </code></sample>
    /// <seealso cref="Session"/>
    public static SessionConsumptionScope Open(Domain domain, SessionConfiguration configuration, bool activate)
    {
      ArgumentValidator.EnsureArgumentNotNull(domain, "domain");
      ArgumentValidator.EnsureArgumentNotNull(configuration, "configuration");

      return domain.OpenSession(configuration, activate);
    }

    #endregion

    /// <inheritdoc/>
    public override string ToString()
    {
      return Name;
    }


    // Constructors

    internal Session(Domain domain, SessionConfiguration configuration)
      : base(domain)
    {
      IsDebugEventLoggingEnabled = Log.IsLogged(LogEventTypes.Debug); // Just to cache this value
      // Both Domain and Configuration are valid references here;
      // Configuration is already locked
      Configuration = configuration;
      Name = configuration.Name;
      // Handlers
      Handlers = domain.Handlers;
      Handler = Handlers.HandlerFactory.CreateHandler<SessionHandler>();
      Handler.Session = this;
      Handler.DefaultIsolationLevel = configuration.DefaultIsolationLevel;
      Handler.Initialize();
      // Caches, registry
      switch (configuration.CacheType) {
      case SessionCacheType.Infinite:
        EntityStateCache = new InfiniteCache<Key, EntityState>(configuration.CacheSize, i => i.Key);
        break;
      default:
        EntityStateCache = new LruCache<Key, EntityState>(configuration.CacheSize, i => i.Key,
          new WeakCache<Key, EntityState>(false, i => i.Key));
        break;
      }
      EntityStateRegistry = new EntityStateRegistry(this, EntityStateRegistrySizeLimit);
      // Etc...
      AtomicityContext = new AtomicityContext(this, AtomicityContextOptions.Undoable);
      CoreServices = new CoreServiceAccessor(this);
      PairSyncManager = new SyncManager(this);
      RemovalProcessor = new RemovalProcessor(this);
      EntityEvents = new EntityEventManager();

      persistRequiresTopologicalSort =
        (Domain.Configuration.ForeignKeyMode & ForeignKeyMode.Reference) > 0 &&
         Domain.Handler.ProviderInfo.SupportsForeignKeyConstraints &&
        !Domain.Handler.ProviderInfo.SupportsDeferredForeignKeyConstraints;
    }

    #region Dispose pattern

    /// <summary>
    /// <see cref="ClassDocTemplate.Dispose" copy="true"/>
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <see cref="ClassDocTemplate.Dtor" copy="true"/>
    /// </summary>
    ~Session()
    {
      Dispose(false);
    }

    private void Dispose(bool disposing)
    {
      if (isDisposed)
        return;
      lock (_lock) {
        if (isDisposed)
          return;
        try {
          if (IsDebugEventLoggingEnabled)
            Log.Debug("Session '{0}'. Disposing.", this);
          NotifyDisposing();
          Handler.DisposeSafely();
        }
        finally {
          isDisposed = true;
        }
      }
    }

    #endregion
  }
}