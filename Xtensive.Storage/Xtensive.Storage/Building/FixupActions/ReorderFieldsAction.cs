// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2009.05.28

using System;
using Xtensive.Storage.Building.Definitions;

namespace Xtensive.Storage.Building.FixupActions
{
  [Serializable]
  internal class ReorderFieldsAction : HierarchyAction
  {
    public override void Run()
    {
      FixupActionProcessor.Process(this);
    }

    public override string ToString()
    {
      return string.Format("Reorder fields in '{0}' type.", Hierarchy.Root.Name);
    }


    // Constructors

    public ReorderFieldsAction(HierarchyDef hierarchy)
      : base(hierarchy)
    {
    }
  }
}