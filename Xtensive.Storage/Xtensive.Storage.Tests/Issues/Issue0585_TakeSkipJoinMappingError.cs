// Copyright (C) 2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Gamzov
// Created:    2010.01.19

using System;
using System.Linq;
using NUnit.Framework;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Tests.Issues.Xtensive.Storage.Tests.Issues.Issue0585_TakeSkipJoinMappingError_Model;

namespace Xtensive.Storage.Tests.Issues
{
  namespace Xtensive.Storage.Tests.Issues.Issue0585_TakeSkipJoinMappingError_Model
  {
    // [Index("Name", Unique = true)]
    // [Index("UniqueIndentifier", Unique = true)]
    [HierarchyRoot]
    public class User : Entity
    {
      [Field, Key]
      public int Id { get; private set; }

      [Field]
      public Guid? UniqueIndentifier { get; set; }

      [Field]
      public string Name { get; set; }

      [Field]
      public string Email { get; set; }

      [Field]
      public string Password { get; set; }

      [Field]
      public string AlternativePassword { get; set; }

      [Field]
      public string PasswordQuestion { get; set; }

      [Field]
      public string PasswordAnswer { get; set; }

      [Field, Association(OnTargetRemove = OnRemoveAction.Clear, PairTo = "Users")]
      public EntitySet<Role> Roles { get; private set; }
    }

    [HierarchyRoot]
    public class UserActivity
      : Entity
    {
      [Field, Key]
      public int Id { get; private set; }

      [Field]
      public DateTime CreationDate { get; set; }

      [Field]
      public DateTime? LastLoginAttemptDate { get; set; }

      [Field]
      public int LoginAttemptCount { get; set; }

      [Field]
      public DateTime LastPasswordChangeDate { get; set; }

      [Field]
      public DateTime LastLockoutDate { get; set; }

      [Field, Association(OnTargetRemove = OnRemoveAction.Cascade)]
      public User User { get; set; }

      [Field]
      public bool IsApproved { get; set; }

      [Field]
      public bool IsLockedOut { get; set; }

      [Field]
      public string Comment { get; set; }

      [Field]
      public DateTime LastLoginDate { get; set; }

      [Field]
      public DateTime LastActivityDate { get; set; }

      public static UserActivity GetOrCreate(User user)
      {
        var activity = Query.All<UserActivity>().Where(ua => ua.User==user).FirstOrDefault();
        if (activity==null)
          activity = new UserActivity {User = user};
        return activity;
      }
    }


    // [Index("Name", Unique = true)]
    [HierarchyRoot]
    public class Role : Entity
    {
      [Field, Key]
      public int Id { get; private set; }

      [Field]
      public string Name { get; set; }

      [Field, Association(OnTargetRemove = OnRemoveAction.Clear, PairTo = "Roles")]
      public EntitySet<User> Users { get; private set; }

      [Field, Association(OnTargetRemove = OnRemoveAction.Clear)]
      public EntitySet<Role> Roles { get; private set; }
    }
  }

  [Serializable]
  public class Issue0585_TakeSkipJoinMappingError : AutoBuildTest
  {
    public override void TestFixtureSetUp()
    {
      base.TestFixtureSetUp();
      using (Session.Open(Domain)) {
        using (var t = Transaction.Open()) {
          Fill();
          t.Complete();
        }
      }
    }

    private void Fill()
    {
      for (int i = 0; i < 10; i++) {
        var user = new User {
          Name = string.Format("name_{0}", i), 
          Password = string.Format("password_{0}", i), 
          PasswordQuestion = string.Format("passwordQuestion_{0}", i), 
          Email = string.Format("email{0}", i)
        };
        for (int j = 0; j < 10; j++) {
          var activity = new UserActivity {
            Comment = string.Format("comment_{0}_{1}", i, j), 
            IsApproved = true, 
            IsLockedOut = false, 
            CreationDate = DateTime.Now, 
            LastLoginDate = DateTime.Now, 
            LastActivityDate = DateTime.Now, 
            LastPasswordChangeDate = DateTime.Now, 
            LastLockoutDate = DateTime.Now,
            User = user
          };
        }
      }
      Session.Current.Persist();
    }

    protected override DomainConfiguration BuildConfiguration()
    {
      var config = base.BuildConfiguration();
      config.Types.Register(typeof (UserActivity).Assembly, typeof (UserActivity).Namespace);
      return config;
    }

    [Test]
    public void MainTest()
    {
      using (Session.Open(Domain)) {
        using (var t = Transaction.Open()) {
          int pageIndex = 1;
          int pageSize = 1;
          IQueryable<User> usersQuery = Query.All<User>().Skip(pageIndex * pageSize).Take(pageSize);
          var query =
            from user in usersQuery
            from activity in Query.All<UserActivity>().Where(a => a.User==user).DefaultIfEmpty()
            select new {
              user.Name,
              user.UniqueIndentifier,
              user.Email,
              user.PasswordQuestion,
              activity.Comment,
              activity.IsApproved,
              activity.IsLockedOut,
              activity.CreationDate,
              activity.LastLoginDate,
              activity.LastActivityDate,
              activity.LastPasswordChangeDate,
              activity.LastLockoutDate
            };
          var result = query.ToList();
          Assert.Greater(0, result.Count);
        }
      }
    }
  }
}