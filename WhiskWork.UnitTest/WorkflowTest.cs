﻿using System;
using System.Collections.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhiskWork.Test.Common;

namespace WhiskWork.Core.UnitTest
{
    [TestClass]
    public class WorkflowTest
    {
        MemoryWorkflowRepository _workflowRepository;
        MemoryWorkItemRepository _workItemRepository;
        Workflow _wp;

        [TestInitialize]
        public void Init()
        {
            _workflowRepository = new MemoryWorkflowRepository();
            _workItemRepository = new MemoryWorkItemRepository();
            _wp = new Workflow(_workflowRepository, _workItemRepository);
        }

        [TestMethod]
        public void ShouldCreateWorkItemInBeginStep()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");
            _workflowRepository.Add("/development", "/", 2, WorkStepType.Normal, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1","/analysis"));
            Assert.AreEqual("/analysis", _workItemRepository.GetWorkItem("cr1").Path);
        }

        [TestMethod]
        public void ShouldNotCreateWorkItemInNormalStep()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");
            _workflowRepository.Add("/development", "/", 2, WorkStepType.Normal, "cr");

            AssertUtils.AssertThrows<InvalidOperationException>(
                () =>
                    {
                        _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
                    });
        }

        [TestMethod]
        public void ShouldCreateWorkItemWithSingleProperty()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1","/analysis",new NameValueCollection { { "Name", "CR1" } }));
            var workItem = _wp.GetWorkItem("cr1");

            Assert.AreEqual(1, workItem.Properties.Count);
            Assert.AreEqual("CR1", workItem.Properties["Name"]);
        }

        [TestMethod]
        public void ShouldCreateWorkItemWithTwoProperties()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1","/analysis",new NameValueCollection { { "Name", "CR1" }, { "Developers", "A, B" } }));
            var workItem = _wp.GetWorkItem("cr1");

            Assert.AreEqual(2, workItem.Properties.Count);
            Assert.AreEqual("CR1", workItem.Properties["Name"]);
            Assert.AreEqual("A, B", workItem.Properties["Developers"]);
        }

        [TestMethod]
        public void ShouldUpdateOneOfTwoProperties()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1","/analysis",new NameValueCollection { { "Name", "CR1" }, { "Developer", "A" } }));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/analysis", new NameValueCollection { { "Developer", "B" } }));
            var workItem = _wp.GetWorkItem("cr1");

            Assert.AreEqual(2, workItem.Properties.Count);
            Assert.AreEqual("CR1", workItem.Properties["Name"]);
            Assert.AreEqual("B", workItem.Properties["Developer"]);
        }

        [TestMethod]
        public void ShouldRemovePropertyIfValueIsEmpty()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1", "/analysis", new NameValueCollection { { "Name", "CR1" }, { "Developer", "A" } }));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/analysis", new NameValueCollection { { "Developer", "" } }));
            var workItem = _wp.GetWorkItem("cr1");

            Assert.AreEqual(1, workItem.Properties.Count);
            Assert.AreEqual("CR1", workItem.Properties["Name"]);
        }


        [TestMethod]
        public void ShouldMoveAndUpdateOneOfTwoProperties()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");
            _workflowRepository.Add("/development", "/", 2, WorkStepType.Normal, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1","/analysis",new NameValueCollection { { "Name", "CR1" }, { "Developer", "A" } }));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/development", new NameValueCollection { { "Developer", "B" } }));
            var workItem = _wp.GetWorkItem("cr1");

            Assert.AreEqual(2, workItem.Properties.Count);
            Assert.AreEqual("/development", workItem.Path);
            Assert.AreEqual("CR1", workItem.Properties["Name"]);
            Assert.AreEqual("B", workItem.Properties["Developer"]);
        }

        [TestMethod]
        public void ShouldUpdatePropertyOfWorkItemInExpandStep()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");
            _workflowRepository.Add("/development", "/", 2, WorkStepType.Normal, "cr");
            _workflowRepository.Add("/development/inprocess", "/development", 1, WorkStepType.Expand, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1", "/analysis"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/development/inprocess"));

            _wp.UpdateWorkItem(WorkItem.New("cr1", "/development/inprocess", new NameValueCollection { { "Developer", "B" } }));

        }

        [TestMethod]
        public void ShouldSetSequentialOrdinalWhenCreatingWorkItemWithoutOrdinal()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1", "/analysis"));
            _wp.CreateWorkItem(WorkItem.New("cr2", "/analysis"));
            _wp.CreateWorkItem(WorkItem.New("cr3", "/analysis"));

            Assert.AreEqual(1, _workItemRepository.GetWorkItem("cr1").Ordinal);
            Assert.AreEqual(2, _workItemRepository.GetWorkItem("cr2").Ordinal);
            Assert.AreEqual(3, _workItemRepository.GetWorkItem("cr3").Ordinal);
        }

        [TestMethod]
        public void ShouldCreateWorkItemWithOrdinalsProvided()
        {
            _workflowRepository.Add("/development", "/", 1, WorkStepType.Begin, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1", "/development").UpdateOrdinal(3));
            _wp.CreateWorkItem(WorkItem.New("cr2", "/development").UpdateOrdinal(2));
            _wp.CreateWorkItem(WorkItem.New("cr3", "/development").UpdateOrdinal(1));

            Assert.AreEqual(3, _workItemRepository.GetWorkItem("cr1").Ordinal);
            Assert.AreEqual(2, _workItemRepository.GetWorkItem("cr2").Ordinal);
            Assert.AreEqual(1, _workItemRepository.GetWorkItem("cr3").Ordinal);
        }

        [TestMethod]
        public void ShouldUpdateWorkItemOrdinalWhenNotMoving()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1","/analysis",new NameValueCollection { { "Name", "CR1" }, { "Developer", "A" } }));
            _wp.UpdateWorkItem(WorkItem.New("cr1","/analysis").UpdateOrdinal(3));
            var workItem = _wp.GetWorkItem("cr1");

            Assert.AreEqual(3, workItem.Ordinal);
        }

        [TestMethod]
        public void ShouldPreserveOrdinalWhenMoving()
        {
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Begin, "cr");
            _workflowRepository.Add("/development", "/", 2, WorkStepType.Normal, "cr");

            _wp.CreateWorkItem(WorkItem.New("cr1", "/analysis").UpdateOrdinal(3));
            _wp.CreateWorkItem(WorkItem.New("cr2", "/analysis").UpdateOrdinal(4));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/development"));

            Assert.AreEqual(3, _wp.GetWorkItem("cr1").Ordinal);
            Assert.AreEqual(4, _wp.GetWorkItem("cr2").Ordinal);
        }
    }
}