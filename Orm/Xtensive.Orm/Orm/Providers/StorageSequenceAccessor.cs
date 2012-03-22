// Copyright (C) 2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexis Kochetov
// Created:    2010.09.13

using System;
using Xtensive.Core;
using Xtensive.IoC;
using Xtensive.Orm.Configuration;
using Xtensive.Orm.Model;
using Xtensive.Orm.Upgrade;
using Xtensive.Sql.Model;

namespace Xtensive.Orm.Providers
{
  /// <summary>
  /// Standard <see cref="IStorageSequenceAccessor"/> implementation.
  /// </summary>
  [Service(typeof (IStorageSequenceAccessor))]
  public class StorageSequenceAccessor : IStorageSequenceAccessor
  {
    private readonly DomainHandler domainHandler;
    private readonly SequenceQueryBuilder queryBuilder;
    private readonly bool hasArbitaryIncrement;
    private readonly bool hasSequences;
    private readonly Domain domain;

    /// <inheritdoc/>
    public Segment<long> NextBulk(SequenceInfo sequenceInfo)
    {
      var generatorNode = GetGeneratorNode(sequenceInfo);
      var query = queryBuilder.Build(generatorNode, sequenceInfo.Increment);

      long hiValue;

      if (UpgradeContext.Current!=null) {
        hiValue = query.ExecuteWith(Session.Demand().Services.Demand<ISqlExecutor>());
      }
      else {
        using (var session = domain.OpenSession(SessionType.KeyGenerator))
        using (session.OpenTransaction()) {
          var executor = session.Services.Demand<ISqlExecutor>();
          hiValue = query.ExecuteWith(executor);
          // Rollback
        }
      }

      var increment = sequenceInfo.Increment;
      var current = hasArbitaryIncrement ? hiValue - increment : (hiValue - 1) * increment;

      return new Segment<long>(current + 1, increment);
    }

    private SchemaNode GetGeneratorNode(SequenceInfo sequenceInfo)
    {
      var result = domainHandler.Mapping[sequenceInfo];
      if (result!=null)
        return result;

      var message = string.Format(
        hasSequences ? Strings.ExSequenceXIsNotFoundInStorage : Strings.ExTableXIsNotFound,
        sequenceInfo.MappingName);
      throw new InvalidOperationException(message);
    }

    // Constructors

    [ServiceConstructor]
    public StorageSequenceAccessor(HandlerAccessor handlers)
    {
      ArgumentValidator.EnsureArgumentNotNull(handlers, "handlers");

      domainHandler = handlers.DomainHandler;
      queryBuilder = handlers.SequenceQueryBuilder;
      domain = handlers.Domain;

      var providerInfo = handlers.ProviderInfo;
      hasArbitaryIncrement =
        providerInfo.Supports(ProviderFeatures.Sequences)
        || providerInfo.Supports(ProviderFeatures.ArbitraryIdentityIncrement);
      hasSequences = providerInfo.Supports(ProviderFeatures.Sequences);
    }
  }
}