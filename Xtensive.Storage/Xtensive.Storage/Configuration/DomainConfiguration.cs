// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2007.08.03

using System;
using System.Configuration;
using System.Web;
using System.Web.Configuration;
using Xtensive.Core;
using Xtensive.Core.Collections;
using Xtensive.Core.Configuration;
using Xtensive.Core.Helpers;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Storage.Configuration.Elements;
using Xtensive.Storage.Configuration.Internals;
using Xtensive.Storage.Resources;
using ConfigurationSection=Xtensive.Storage.Configuration.Elements.ConfigurationSection;

namespace Xtensive.Storage.Configuration
{
  /// <summary>
  /// The configuration of the <see cref="Domain"/>.
  /// </summary> 
  [Serializable]
  public class  DomainConfiguration : ConfigurationBase
  {
    #region Defaults (constants)

    /// <summary>
    /// Default <see cref="UpgradeMode"/> value:
    /// "<see cref="DomainUpgradeMode.Default" />".
    /// </summary>
    public const DomainUpgradeMode DefaultUpgradeMode = DomainUpgradeMode.Default;

    /// <summary>
    /// Default <see cref="ForeignKeyMode"/> value:
    /// "<see cref="Storage.ForeignKeyMode.Default" />".
    /// </summary>
    public const ForeignKeyMode DefaultForeignKeyMode = ForeignKeyMode.Default;

    /// <summary>
    /// Default <see cref="SectionName"/> value:
    /// "<see langword="Xtensive.Storage" />".
    /// </summary>
    public static string DefaultSectionName = "Xtensive.Storage";

    /// <summary>
    /// Default <see cref="DomainConfiguration.KeyCacheSize"/> value: 
    /// <see langword="16*1024" />.
    /// </summary>
    public const int DefaultKeyCacheSize = 16*1024;

    /// <summary>
    /// Default <see cref="DomainConfiguration.KeyGeneratorCacheSize"/> value: 
    /// <see langword="128" />.
    /// </summary>
    public const int DefaultKeyGeneratorCacheSize = 128;

    /// <summary>
    /// Default <see cref="DomainConfiguration.QueryCacheSize"/> value: 
    /// <see langword="1024" />.
    /// </summary>
    public const int DefaultQueryCacheSize = 1024;

    /// <summary>
    /// Default <see cref="DomainConfiguration.RecordSetMappingCacheSize"/> value: 
    /// <see langword="1024" />.
    /// </summary>
    public const int DefaultRecordSetMappingCacheSize = 1024;

    /// <summary>
    /// Default <see cref="DomainConfiguration.SessionPoolSize"/> value: 
    /// <see langword="64" />.
    /// </summary>
    public const int DefaultSessionPoolSize = 64;

    /// <summary>
    /// Default <see cref="DomainConfiguration.InconsistentTransactions"/> value: 
    /// <see langword="false" />.
    /// </summary>
    public const bool DefaultInconsistentTransactions = false;

    /// <summary>
    /// Default <see cref="DomainConfiguration.AutoValidation"/> value: 
    /// <see langword="true" />.
    /// </summary>
    public const bool DefaultAutoValidation = true;

    #endregion
    
    private static string sectionName = DefaultSectionName;
    private static bool sectionNameIsDefined;

    private string name = string.Empty;
    private UrlInfo connectionInfo;
    private string defaultSchema = string.Empty;
    private TypeRegistry types = new TypeRegistry(new SessionBoundTypeRegistrationHandler());
    private TypeRegistry compilerContainers = new TypeRegistry(new CompilerContainerRegistrationHandler());
    private NamingConvention namingConvention = new NamingConvention();
    private int keyCacheSize = DefaultKeyCacheSize;
    private int keyGeneratorCacheSize = DefaultKeyGeneratorCacheSize;
    private int queryCacheSize = DefaultQueryCacheSize;
    private int recordSetMappingCacheSize = DefaultRecordSetMappingCacheSize;
    private bool autoValidation = true;
    private bool inconsistentTransactions;
    private SessionConfigurationCollection sessions = new SessionConfigurationCollection();
    private DomainUpgradeMode upgradeMode = DefaultUpgradeMode;
    private ForeignKeyMode foreignKeyMode = DefaultForeignKeyMode;
    private ValidationMode validationMode;

    /// <summary>
    /// Gets or sets the name of the section where storage configuration is configuration.
    /// </summary>
    /// <exception cref="NotSupportedException">The property is already defined once.</exception>
    public static string SectionName
    {
      get { return sectionName; }
      set
      {
        ArgumentValidator.EnsureArgumentNotNullOrEmpty(value, "value");
        if (sectionNameIsDefined)
          throw Exceptions.AlreadyInitialized("SectionName");
        sectionName = value;
        sectionNameIsDefined = true;
      }
    }

    /// <summary>
    /// Gets or sets the domain configuration name.
    /// </summary>
    public string Name
    {
      get { return name; }
      set
      {
        this.EnsureNotLocked();
        ArgumentValidator.EnsureArgumentNotNull(value, "Name");
        name = value;
      }
    }

    /// <summary>
    /// Gets or sets the connection info (URL).
    /// </summary>
    /// <example>
    /// <code lang="cs" source="..\Xtensive.Storage\Xtensive.Storage.Manual\DomainAndSession\DomainAndSessionSample.cs" region="Connection URL examples" />
    /// <code lang="cs">
    /// var configuration = new DomainConfiguration();
    /// configuration.ConnectionInfo = new UrlInfo(connectionUrl);
    /// </code>
    /// </example>
    public UrlInfo ConnectionInfo
    {
      get { return connectionInfo; }
      set
      {
        this.EnsureNotLocked();
        connectionInfo = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating domain upgrade behavior. 
    /// Default value is <see cref="DefaultUpgradeMode"/>.
    /// </summary>
    public DomainUpgradeMode UpgradeMode
    {
      get { return upgradeMode; }
      set
      {
        this.EnsureNotLocked();
        upgradeMode = value;
      }
    }

    /// <summary>
    /// Gets the collection of persistent <see cref="Type"/>s that are about to be 
    /// registered in the <see cref="Domain"/>.
    /// </summary>
    public TypeRegistry Types { get { return types; } }
    
    /// <summary>
    /// Gets or sets the naming convention.
    /// </summary>
    public NamingConvention NamingConvention
    {
      get { return namingConvention; }
      set {
        this.EnsureNotLocked();
        namingConvention = value;
      }
    }

    /// <summary>
    /// Gets or sets the size of the key cache.
    /// Default value is <see cref="DefaultKeyCacheSize"/>.
    /// </summary>
    public int KeyCacheSize
    {
      get { return keyCacheSize; }
      set {
        this.EnsureNotLocked();
        ArgumentValidator.EnsureArgumentIsGreaterThan(value, 0, "value");
        keyCacheSize = value;
      }
    }

    /// <summary>
    /// Gets or sets the size of the key generator cache.
    /// Default value is <see cref="DefaultKeyGeneratorCacheSize"/>.
    /// </summary>
    public int KeyGeneratorCacheSize
    {
      get { return keyGeneratorCacheSize; }
      set {
        this.EnsureNotLocked();
        ArgumentValidator.EnsureArgumentIsGreaterThan(value, 0, "value");
        keyGeneratorCacheSize = value;
      }
    }

    /// <summary>
    /// Gets or sets the size of the query cache (see <see cref="CachedQuery"/>).
    /// Default value is <see cref="DefaultQueryCacheSize"/>.
    /// </summary>
    public int QueryCacheSize
    {
      get { return queryCacheSize; }
      set {
        this.EnsureNotLocked();
        ArgumentValidator.EnsureArgumentIsGreaterThan(value, 0, "value");
        queryCacheSize = value;
      }
    }

    /// <summary>
    /// Gets or sets the size of the record set mapping cache.
    /// Default value is <see cref="DefaultRecordSetMappingCacheSize"/>.
    /// </summary>
    public int RecordSetMappingCacheSize
    {
      get { return recordSetMappingCacheSize; }
      set {
        this.EnsureNotLocked();
        ArgumentValidator.EnsureArgumentIsGreaterThan(value, 0, "value");
        recordSetMappingCacheSize = value;
      }
    }

    /// <summary>
    /// Gets or sets the value indicating whether changed entities should be validated or registered for validation automatically.
    /// Default value is <see cref="DomainConfigurationElement.AutoValidation"/>.
    /// </summary>
    public bool AutoValidation
    {
      get { return autoValidation; }
      set {
        this.EnsureNotLocked();
        autoValidation = value;
      }
    }

    /// <summary>
    /// Gets or sets the validation mode, that is used for validating entities within transactions.
    /// </summary>
    public ValidationMode ValidationMode
    {
      get { return validationMode; }
      set {
        this.EnsureNotLocked();
        validationMode = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating foreign key mode. 
    /// Default value is <see cref="DefaultForeignKeyMode"/>.
    /// </summary>
    public ForeignKeyMode ForeignKeyMode
    {
      get { return foreignKeyMode; }
      set {
        this.EnsureNotLocked();
        foreignKeyMode = value;
      }
    }

    /// <summary>
    /// Gets available session configurations.
    /// </summary>
    public SessionConfigurationCollection Sessions
    {
      get { return sessions; }
      set {
        ArgumentValidator.EnsureArgumentNotNull(value, "value");
        this.EnsureNotLocked();
        sessions = value;
      }
    }

    /// <summary>
    /// Gets user defined method compiler containers.
    /// </summary>
    public TypeRegistry CompilerContainers { get { return compilerContainers; } }

    /// <summary>
    /// Gets or sets the default schema.
    /// </summary>
    public string DefaultSchema
    {
      get { return defaultSchema; }
      set {
        this.EnsureNotLocked();
        defaultSchema = value;
      }
    }

    /// <summary>
    /// Locks the instance and (possible) all dependent objects.
    /// </summary>
    /// <param name="recursive"><see langword="True"/> if all dependent objects should be locked as well.</param>
    public override void Lock(bool recursive)
    {
      types.Lock(true);
      sessions.Lock(true);
      compilerContainers.Lock(true);
      base.Lock(recursive);
    }

    /// <inheritdoc/>
    public override void Validate()
    {
    }

    /// <inheritdoc/>
    protected override ConfigurationBase CreateClone()
    {
      return new DomainConfiguration();
    }

    /// <summary>
    /// Copies the properties from the <paramref name="source"/>
    /// configuration to this one.
    /// Used by <see cref="ConfigurationBase.Clone"/> method implementation.
    /// </summary>
    /// <param name="source">The configuration to copy properties from.</param>
    /// <inheritdoc/>
    protected override void Clone(ConfigurationBase source)
    {
      base.Clone(source);
      var configuration = (DomainConfiguration) source;
      name = configuration.Name;
      connectionInfo = configuration.ConnectionInfo;
      defaultSchema = configuration.defaultSchema;
      types = (TypeRegistry) configuration.Types.Clone();
      namingConvention = (NamingConvention) configuration.NamingConvention.Clone();
      keyCacheSize = configuration.KeyCacheSize;
      keyGeneratorCacheSize = configuration.KeyGeneratorCacheSize;
      queryCacheSize = configuration.QueryCacheSize;
      recordSetMappingCacheSize = configuration.RecordSetMappingCacheSize;
      sessions = (SessionConfigurationCollection) configuration.Sessions.Clone();
      compilerContainers = (TypeRegistry) configuration.CompilerContainers.Clone();
      upgradeMode = configuration.upgradeMode;
      foreignKeyMode = configuration.foreignKeyMode;
      validationMode = configuration.validationMode;
    }

    /// <summary>
    /// Clones this instance.
    /// </summary>
    /// <returns>The clone of this configuration.</returns>
    public new DomainConfiguration Clone()
    {
      return (DomainConfiguration) base.Clone();
    }

    /// <summary>
    /// Loads the <see cref="DomainConfiguration"/> for <see cref="Domain"/>
    /// with the specified <paramref name="name"/>
    /// from application configuration file (section with <see cref="SectionName"/>).
    /// </summary>
    /// <param name="name">Name of the <see cref="Domain"/>.</param>
    /// <returns>
    /// The <see cref="DomainConfiguration"/> for the specified domain.
    /// </returns>
    /// <exception cref="InvalidOperationException">Section <see cref="SectionName"/>
    /// is not found in application configuration file, or there is no configuration for
    /// the <see cref="Domain"/> with specified <paramref name="name"/>.</exception>
    public static DomainConfiguration Load(string name)
    {
      return Load(SectionName, name);
    }

    /// <summary>
    /// Loads the <see cref="DomainConfiguration"/> for <see cref="Domain"/>
    /// with the specified <paramref name="name"/>
    /// from application configuration file (section with <paramref name="sectionName"/>).
    /// </summary>
    /// <param name="sectionName">Name of the section.</param>
    /// <param name="name">Name of the <see cref="Domain"/>.</param>
    /// <returns>
    /// The <see cref="DomainConfiguration"/> for the specified domain.
    /// </returns>
    /// <exception cref="InvalidOperationException">Section <paramref name="sectionName"/>
    /// is not found in application configuration file, or there is no configuration for
    /// the <see cref="Domain"/> with specified <paramref name="name"/>.</exception>
    public static DomainConfiguration Load(string sectionName, string name)
    {
      ConfigurationSection section;
      if (HttpContext.Current!=null) {
        // See http://code.google.com/p/dataobjectsdotnet/issues/detail?id=459
        // (workaround for IIS 7 @ 64 bit Windows Server 2008)
        var config = WebConfigurationManager.OpenWebConfiguration("~");
        section = (ConfigurationSection) config.GetSection(sectionName);
      }
      else
        section = (ConfigurationSection) ConfigurationManager.GetSection(sectionName);
      if (section==null) 
        throw new InvalidOperationException(string.Format(
          Strings.ExSectionIsNotFoundInApplicationConfigurationFile, sectionName));
      var domainElement = section.Domains[name];
      if (domainElement==null)
        throw new InvalidOperationException(string.Format(
          Strings.ExConfigurationForDomainIsNotFoundInApplicationConfigurationFile, name, sectionName));
      return domainElement.ToNative();
    }


    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="connectionUrl">The string containing connection URL for <see cref="Domain"/>.</param>
    /// <exception cref="ArgumentNullException">Parameter <paramref name="connectionUrl"/> is null or empty string.</exception>
    public DomainConfiguration(string connectionUrl)
      : this()
    {
      ArgumentValidator.EnsureArgumentNotNull(connectionUrl, "connectionUrl");
      connectionInfo = UrlInfo.Parse(connectionUrl);
    }

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    public DomainConfiguration()
    {
      // This assembly must be always registered
      types.Register(typeof (Persistent).Assembly);
    }
  }
}
