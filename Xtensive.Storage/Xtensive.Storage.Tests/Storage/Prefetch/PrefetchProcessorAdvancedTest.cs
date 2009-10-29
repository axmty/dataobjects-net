// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2009.10.26

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xtensive.Core.Collections;
using Xtensive.Storage.Internals;
using Xtensive.Storage.Internals.Prefetch;
using Xtensive.Storage.Providers;
using Xtensive.Storage.Tests.Storage.Prefetch.Model;

namespace Xtensive.Storage.Tests.Storage.Prefetch
{
  [TestFixture]
  public sealed class PrefetchProcessorAdvancedTest : PrefetchProcessorTestBase
  {
    [Test]
    public void OwnerOfReferencedEntitiesIsNotFoundTest()
    {
      Key orderKey;
      using (Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var order = Query<Order>.All.OrderBy(c => c.Id).First();
        orderKey = order.Key;
        order.Remove();
        tx.Complete();
      }

      using (var session = Session.Open(Domain))
      using (Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(Session.Demand());
        prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(CustomerField, true));
        var taskContainer = GetSingleTaskContainer(prefetchProcessor);
        prefetchProcessor.ExecuteTasks();
        var referncedEntityTask = taskContainer.ReferencedEntityContainers.Single();
        Assert.IsNotNull(taskContainer.RootEntityContainer.Task);
        Assert.IsNull(referncedEntityTask.Task);
        var state = session.EntityStateCache[orderKey, true];
        Assert.IsNotNull(state);
        Assert.AreEqual(PersistenceState.Synchronized, state.PersistenceState);
        Assert.IsNull(state.Tuple);
      }
    }

    [Test]
    public void EntityHaveBeenLoadedBeforeTaskActivationTest()
    {
      var customerKey = GetFirstKey<Customer>();

      using (var session = Session.Open(Domain))
      using (Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(Session.Demand());
        prefetchProcessor.Prefetch(customerKey, null, new PrefetchFieldDescriptor(CityField));
        prefetchProcessor.ExecuteTasks();

        prefetchProcessor.Prefetch(customerKey, null, new PrefetchFieldDescriptor(PersonIdField),
          new PrefetchFieldDescriptor(CityField));
        var taskContainer = GetSingleTaskContainer(prefetchProcessor);
        taskContainer.RootEntityContainer.GetTask();
        Assert.IsNull(taskContainer.RootEntityContainer.Task);
      }
    }

    [Test]
    public void ReferencedEntityHaveBeenLoadedBeforeTaskActivationTest()
    {
      Key order0Key;
      Key employee0Key;
      Key order1Key;
      Key employee1Key;
      using (Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var order0 = Query<Order>.All.OrderBy(o => o.Id).First();
        var order1 = Query<Order>.All.OrderBy(o => o.Id).Skip(1).First();
        order0Key = order0.Key;
        employee0Key = order0.Employee.Key;
        order1Key = order1.Key;
        employee1Key = order1.Employee.Key;
      }

      using (var session = Session.Open(Domain))
      using (Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(Session.Demand());
        var employeeNameField = Domain.Model.Types[typeof (Person)].Fields["Name"];
        var employeeAgeField = employee1Key.Type.Fields["Age"];
        prefetchProcessor.Prefetch(employee0Key, null,
          new PrefetchFieldDescriptor(employeeNameField), new PrefetchFieldDescriptor(AgeField));
        prefetchProcessor.Prefetch(employee1Key, null, new PrefetchFieldDescriptor(AgeField));
        prefetchProcessor.Prefetch(order0Key, null, new PrefetchFieldDescriptor(OrderIdField));
        prefetchProcessor.Prefetch(order1Key, null, new PrefetchFieldDescriptor(OrderIdField));
        prefetchProcessor.ExecuteTasks();

        prefetchProcessor.Prefetch(order0Key, null, new PrefetchFieldDescriptor(EmployeeField, true));
        prefetchProcessor.Prefetch(order1Key, null, new PrefetchFieldDescriptor(EmployeeField, true));
        var taskContainers = (SetSlim<GraphContainer>) GraphContainersField.GetValue(prefetchProcessor);
        Assert.AreEqual(2, taskContainers.Count);
        Func<Key, ReferencedEntityContainer> taskSelector = containerKey => taskContainers
          .Where(container => container.Key==containerKey)
          .SelectMany(container => container.ReferencedEntityContainers).Single();
        var task0 = taskSelector.Invoke(order0Key);
        var task1 = taskSelector.Invoke(order1Key);
        prefetchProcessor.ExecuteTasks();
        Assert.IsNull(task0.Task);
        Assert.IsNotNull(task1.Task);
        PrefetchTestHelper.AssertOnlySpecifiedColumnsAreLoaded(employee0Key, employee0Key.Type, session,
          PrefetchTestHelper.IsFieldToBeLoadedByDefault);
        PrefetchTestHelper.AssertOnlySpecifiedColumnsAreLoaded(employee1Key, employee1Key.Type, session,
          PrefetchTestHelper.IsFieldToBeLoadedByDefault);
      }
    }

    [Test]
    public void PrefetchEmptyEntitySetTest()
    {
      Key orderKey;
      using (Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var order = Query<Order>.All.OrderBy(o => o.Id).First();
        var newOrder = new Order {Employee = null, Customer = order.Customer};
        orderKey = newOrder.Key;
        tx.Complete();
      }

      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var prefetchProcessor = (PrefetchProcessor) PrefetchProcessorField.GetValue(session.Handler);
        session.Handler.FetchInstance(orderKey);
        prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(DetailsField, null));
        var taskContainers = (SetSlim<GraphContainer>) GraphContainersField.GetValue(prefetchProcessor);
        Assert.AreEqual(1, taskContainers.Count);
        prefetchProcessor.ExecuteTasks();
        EntitySetState actualState;
        session.Handler.TryGetEntitySetState(orderKey, DetailsField, out actualState);
        Assert.AreEqual(0, actualState.Count);
        Assert.IsTrue(actualState.IsFullyLoaded);
      }
    }

    [Test]
    public void PrefetchReferencedEntityWhenTypeSpecifiedForOwnerIsInvalidTest()
    {
      var productKey = GetFirstKey<Product>();

      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var keyWithoutType = Key.Create(Domain, productKey.TypeRef.Type, TypeReferenceAccuracy.BaseType, productKey.Value);
        var prefetchProcessor = new PrefetchProcessor(Session.Demand());
        prefetchProcessor.Prefetch(keyWithoutType, Domain.Model.Types[typeof (PersonalProduct)],
          new PrefetchFieldDescriptor(Domain.Model.Types[typeof (PersonalProduct)].Fields["Employee"], true));
        var taskContainers = (SetSlim<GraphContainer>) GraphContainersField.GetValue(prefetchProcessor);
        var task = taskContainers.Where(container => container.ReferencedEntityContainers!=null)
          .Single().ReferencedEntityContainers.Single();
        prefetchProcessor.ExecuteTasks();
        Assert.IsNull(task.Task);
      }
    }

    [Test]
    public void DeletingOfTasksAtTransactionCommitOrRollbackTest()
    {
      Key orderKey = GetFirstKey<Order>();

      using (var session = Session.Open(Domain)) {
        var prefetchProcessor = (PrefetchProcessor) PrefetchProcessorField.GetValue(session.Handler);
        SetSlim<GraphContainer> taskContainers;
        using (var tx = Transaction.Open()) {
          prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(CustomerField));
          tx.Complete();
          taskContainers = (SetSlim<GraphContainer>) GraphContainersField.GetValue(prefetchProcessor);
          Assert.AreEqual(1, taskContainers.Count);
        }
        Assert.AreEqual(0, taskContainers.Count);

        using (var tx = Transaction.Open()) {
          prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(EmployeeField));
          Assert.AreEqual(1, taskContainers.Count);
        }
        Assert.AreEqual(0, taskContainers.Count);
      }
    }

    [Test]
    public void TasksAreExecutedAutomaticallyWhenTheirCountIsReachedLimitTest()
    {
      const int entityCount = 101;
      var keys = new List<Key>(entityCount);
      using (Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        for (int i = 0; i < entityCount; i++)
          keys.Add(new Book().Key);
        tx.Complete();
      }

      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(session);
        var taskContainers = (SetSlim<GraphContainer>) GraphContainersField.GetValue(prefetchProcessor);
        var idField = BookType.Fields["Id"];
        for (var i = 1; i < keys.Count; i++) {
          prefetchProcessor.Prefetch(keys[i - 1], null, new PrefetchFieldDescriptor(idField));
          Assert.AreEqual(i % entityCount, taskContainers.Count);
        }
        prefetchProcessor.Prefetch(keys[entityCount - 1], null, new PrefetchFieldDescriptor(idField));
        Assert.AreEqual(1, taskContainers.Count);
        for (var i = 0; i < entityCount - 1; i++)
          PrefetchTestHelper.AssertOnlySpecifiedColumnsAreLoaded(keys[i], BookType, session, IsFieldKeyOrSystem);
      }
    }

    [Test]
    public void RepeatedRegistrationOfReferencingFieldTest()
    {
      var orderKey = GetFirstKey<Order>();

      using (var session = Session.Open(Domain)) {
        using (var tx = Transaction.Open()) {
          var prefetchProcessor = new PrefetchProcessor(session);
          prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(CustomerField));
          prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(CustomerField, true));
          prefetchProcessor.ExecuteTasks();
          var orderState = session.EntityStateCache[orderKey, true];
          var customerKey = Key.Create(Domain, typeof(Customer).GetTypeInfo(Domain),
            TypeReferenceAccuracy.ExactType, CustomerField.Association.ExtractForeignKey(orderState.Tuple));
          PrefetchTestHelper.AssertOnlySpecifiedColumnsAreLoaded(customerKey, CustomerType, session, PrefetchTestHelper.IsFieldToBeLoadedByDefault);
        }
      }
    }

    [Test]
    public void RepeatedRegistrationOfEntitySetFieldTest()
    {
      Key orderKey;
      using (Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var order = new Order {Number = 1, Customer = null, Employee = null};
        orderKey = order.Key;
        new OrderDetail {Order = order, Product = null, Count = 100};
        new OrderDetail {Order = order, Product = null, Count = 200};
        new OrderDetail {Order = order, Product = null, Count = 300};
        new OrderDetail {Order = order, Product = null, Count = 400};
        tx.Complete();
      }

      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(session);
        prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(DetailsField, 1));
        prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(DetailsField, null));
        prefetchProcessor.ExecuteTasks();
        EntitySetState entitySetState;
        Assert.IsTrue(session.Handler.TryGetEntitySetState(orderKey, DetailsField, out entitySetState));
        Assert.IsTrue(entitySetState.IsFullyLoaded);
      }

      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(session);
        prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(DetailsField, 1));
        prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(DetailsField, 2));
        prefetchProcessor.ExecuteTasks();
        EntitySetState entitySetState;
        Assert.IsTrue(session.Handler.TryGetEntitySetState(orderKey, DetailsField, out entitySetState));
        Assert.AreEqual(2, entitySetState.Count());
        Assert.IsFalse(entitySetState.IsFullyLoaded);
      }
    }

    [Test]
    public void FetchInstanceTest()
    {
      var orderKey = GetFirstKey<Order>();

      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var fetchedEntityState = session.Handler.FetchInstance(orderKey);
        PrefetchTestHelper.AssertOnlySpecifiedColumnsAreLoaded(orderKey, OrderType, session,
          PrefetchTestHelper.IsFieldToBeLoadedByDefault);
      }
    }

    [Test]
    public void QueryPlanReusingTest()
    {
      Key customer0Key;
      Key customer1Key;
      Key order0Key;
      Key order1Key;
      using (Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        customer0Key = Query<Customer>.All.OrderBy(c => c.Id).First().Key;
        customer1Key = Query<Customer>.All.OrderBy(c => c.Id).Skip(1).First().Key;
        order0Key = Query<Order>.All.OrderBy(o => o.Id).First().Key;
        order1Key = Query<Order>.All.OrderBy(o => o.Id).Skip(1).First().Key;
      }

      using (var session = Session.Open(Domain))
      using (Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(Session.Demand());
        prefetchProcessor.Prefetch(customer0Key, null, new PrefetchFieldDescriptor(CityField));
        prefetchProcessor.ExecuteTasks();
        var cache = (IEnumerable) CompilationContextCacheField.GetValue(CompilationContext.Current);
        var expectedCachedProviders = cache.Cast<object>().ToList();
        
        prefetchProcessor.Prefetch(customer1Key, null, new PrefetchFieldDescriptor(CityField));
        prefetchProcessor.ExecuteTasks();
        Assert.IsTrue(expectedCachedProviders.SequenceEqual(cache.Cast<object>()));
      }

      using (var session = Session.Open(Domain))
      using (Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(Session.Demand());
        prefetchProcessor.Prefetch(order0Key, null, new PrefetchFieldDescriptor(DetailsField));
        prefetchProcessor.ExecuteTasks();
        var cache = (IEnumerable) CompilationContextCacheField.GetValue(CompilationContext.Current);
        var expectedCachedProviders = cache.Cast<object>().ToList();
        
        prefetchProcessor.Prefetch(order1Key, null, new PrefetchFieldDescriptor(DetailsField));
        prefetchProcessor.ExecuteTasks();
        Assert.IsTrue(expectedCachedProviders.SequenceEqual(cache.Cast<object>()));
      }
    }

    [Test]
    public void EntitySetQueryPlanReusingTest()
    {
      Key order0Key;
      Key author0Key;
      Key order1Key;
      Key author1Key;
      using (Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var order0 = new Order {Number = 1, Customer = null, Employee = null};
        order0Key = order0.Key;
        var order0Detail1 = new OrderDetail {Order = order0, Product = null, Count = 100};
        var order0Detail2 = new OrderDetail {Order = order0, Product = null, Count = 200};
        var order0Detail3 = new OrderDetail {Order = order0, Product = null, Count = 300};
        var order0Detail4 = new OrderDetail {Order = order0, Product = null, Count = 400};

        var order1 = new Order {Number = 1, Customer = null, Employee = null};
        order1Key = order1.Key;
        var order1Detail1 = new OrderDetail {Order = order1, Product = null, Count = 100};
        var order1Detail2 = new OrderDetail {Order = order1, Product = null, Count = 200};
        var order1Detail3 = new OrderDetail {Order = order1, Product = null, Count = 300};
        var order1Detail4 = new OrderDetail {Order = order1, Product = null, Count = 400};

        tx.Complete();
      }

      using (var session = Session.Open(Domain))
      using (Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(Session.Demand());
        prefetchProcessor.Prefetch(order0Key, null, new PrefetchFieldDescriptor(DetailsField, 3));
        prefetchProcessor.ExecuteTasks();
        var cache = (IEnumerable) CompilationContextCacheField.GetValue(CompilationContext.Current);
        var expectedCachedProviders = cache.Cast<object>().ToList();
        
        prefetchProcessor.Prefetch(order1Key, null, new PrefetchFieldDescriptor(DetailsField, 2));
        prefetchProcessor.ExecuteTasks();
        ValidateLoadedEntitySet(order0Key, DetailsField, 3, false, session);
        ValidateLoadedEntitySet(order1Key, DetailsField, 2, false, session);
        Assert.IsTrue(expectedCachedProviders.SequenceEqual(cache.Cast<object>()));
      }

      using (var session = Session.Open(Domain))
      using (Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(Session.Demand());
        prefetchProcessor.Prefetch(order0Key, null, new PrefetchFieldDescriptor(DetailsField));
        prefetchProcessor.ExecuteTasks();
        var cache = (IEnumerable) CompilationContextCacheField.GetValue(CompilationContext.Current);
        var expectedCachedProviders = cache.Cast<object>().ToList();
        
        prefetchProcessor.Prefetch(order1Key, null, new PrefetchFieldDescriptor(DetailsField));
        prefetchProcessor.ExecuteTasks();
        ValidateLoadedEntitySet(order0Key, DetailsField, 4, true, session);
        ValidateLoadedEntitySet(order1Key, DetailsField, 4, true, session);
        Assert.IsTrue(expectedCachedProviders.SequenceEqual(cache.Cast<object>()));
      }
    }

    [Test]
    public void PutNullInCacheIfEntityIsNotFoundTest()
    {
      Key orderKey;
      using (Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var order = Query<Order>.All.OrderBy(c => c.Id).First();
        orderKey = order.Key;
        order.Remove();
        tx.Complete();
      }

      using (var session = Session.Open(Domain))
      using (Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(Session.Demand());
        var numberField = typeof (Order).GetTypeInfo().Fields["Number"];
        prefetchProcessor.Prefetch(orderKey, null, new PrefetchFieldDescriptor(numberField));
        prefetchProcessor.ExecuteTasks();

        var state = session.EntityStateCache[orderKey, true];
        Assert.IsNull(state.Tuple);
        Assert.AreEqual(PersistenceState.Synchronized, state.PersistenceState);
      }
    }

    [Test]
    public void PrefetchViaReferenceToSelfTest()
    {
      Key key;
      using (Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var referenceToSelf = new ReferenceToSelf {AuxField = 100};
        key = referenceToSelf.Key;
        referenceToSelf.Reference = referenceToSelf;
        tx.Complete();
      }

      using (var session = Session.Open(Domain))
      using (Transaction.Open()) {
        var prefetchProcessor = new PrefetchProcessor(Session.Demand());
        var referenceField = typeof (ReferenceToSelf).GetTypeInfo().Fields["Reference"];
        prefetchProcessor.Prefetch(key, null, new PrefetchFieldDescriptor(referenceField, true));
        prefetchProcessor.ExecuteTasks();
        PrefetchTestHelper.AssertOnlySpecifiedColumnsAreLoaded(key, key.Type, session,
          PrefetchTestHelper.IsFieldToBeLoadedByDefault);
        PrefetchTestHelper.AssertReferencedEntityIsLoaded(key, session, referenceField);
      }
    }
  }
}