// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexis Kochetov
// Created:    2009.02.03

using NUnit.Framework;
using Xtensive.Core.Disposing;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Tests.ObjectModel.NorthwindDO;

namespace Xtensive.Storage.Tests.ObjectModel
{
  [TestFixture]
  public abstract class NorthwindDOModelTest : AutoBuildTest
  {
    private DisposableSet disposables;
    private bool firstRun = true;

    [SetUp]
    public virtual void SetUp()
    {
      if (firstRun) {
        firstRun = false;
        if (Domain == null)
          TestFixtureSetUp();
      }
      disposables = new DisposableSet();
      disposables.Add(Domain.OpenSession());
      disposables.Add(Transaction.Open());
    }

    [TearDown]
    public virtual void TearDown()
    {
      disposables.DisposeSafely();
    }

    protected override DomainConfiguration BuildConfiguration()
    {
      var config = base.BuildConfiguration();
      config.UpgradeMode = DomainUpgradeMode.Recreate;
      config.Types.Register(typeof(Supplier).Assembly, typeof(Supplier).Namespace);
      return config;
    }

    protected override Domain BuildDomain(DomainConfiguration configuration)
    {
      Domain domain = base.BuildDomain(configuration);
      DataBaseFiller.Fill(domain);
      return domain;
    }
    
  }
}