// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2009.04.22

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rhino.Mocks;
using Xtensive.Storage.Model;
using Xtensive.Storage.Rse.Compilation;
using Xtensive.Storage.Rse.Providers;
using Xtensive.Storage.Rse.Providers.Compilable;

namespace Xtensive.Storage.Tests.Rse
{
  public class IndexOptimizerTestHelper
  {
    public static IndexInfo GetIndexForField<T>(string fieldName, DomainModel domainModel)
    {
      var targetName = "_" + fieldName;
      return domainModel.Types[typeof(T)].Fields[fieldName].Column.Indexes.First(
        idx => idx.ShortName.EndsWith(targetName));
    }

    public static IndexInfo GetIndexForForeignKey<T>(string fieldName, DomainModel domainModel)
    {
      return domainModel.Types[typeof(T)].Fields[fieldName].Fields[fieldName + ".Id"]
        .Column.Indexes.First();
    }

    public static void ValidateQueryResult<T>(IEnumerable<T> expected, IEnumerable<T> actual)
      where T : Entity
    {
      Assert.Greater(expected.Count(), 0);
      var equalityComparer = MockRepository.GenerateStub<IEqualityComparer<T>>();
      equalityComparer.Stub(comparer => comparer.Equals(Arg<T>.Is.Anything, Arg<T>.Is.Anything))
        .Return(false).WhenCalled(invocation =>
          invocation.ReturnValue = ((Entity)invocation.Arguments[0]).Key
            == ((Entity)invocation.Arguments[1]).Key);
      Assert.AreEqual(0, expected.Except(actual, equalityComparer).Count());
    }

    public static void ValidateUsedIndex<T>(IQueryable<T> query, DomainModel domainModel,
      params IndexInfo[] expectedIndexes)
    {
      var optimizedProvider = GetOptimizedProvider(query);
      var secondaryIndexProviders = new List<IndexInfo>();
      FindSecondaryIndexProviders(optimizedProvider, secondaryIndexProviders, domainModel);
      Assert.Greater(secondaryIndexProviders.Count, 0);
      Assert.AreEqual(0, secondaryIndexProviders.Except(expectedIndexes).Count());
    }

    public static CompilableProvider GetOptimizedProvider<T>(IQueryable<T> query)
    {
      CompilableProvider optimizedProvider;
      using (EnumerationScope.Open()) {
        var recordSet = ((Query<T>) query).Compiled;
        optimizedProvider = CompilationContext.Current.Compile(recordSet.Provider).Origin;
      }
      return optimizedProvider;
    }

    public static void FindSecondaryIndexProviders(Provider provider, List<IndexInfo> result,
      DomainModel domainModel)
    {
      foreach (var source in provider.Sources) {
        var indexProvider = source as IndexProvider;
        if (indexProvider!=null) {
          var indexInfo = indexProvider.Index.Resolve(domainModel);
          if (!indexInfo.IsPrimary)
            result.Add(indexInfo);
        }
        else
          FindSecondaryIndexProviders(source, result, domainModel);
      }
    }
  }
}