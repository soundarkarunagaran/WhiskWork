﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhiskWork.Generic;
using WhiskWork.Test.Common;

namespace WhiskWork.Core.UnitTest
{
    [TestClass]
    public class ParallelWorkStepsTest
    {
        private MemoryWorkflowRepository _workflowRepository;
        private MemoryWorkItemRepository _workItemRepository;
        private Workflow _wp;

        [TestInitialize]
        public void Init()
        {
            _workflowRepository = new MemoryWorkflowRepository();
            _workItemRepository = new MemoryWorkItemRepository();
            _wp = new Workflow(_workflowRepository, _workItemRepository);
        }

        [TestMethod]
        public void ShouldGetAllSubsteps()
        {
            _workflowRepository.Add("/feedback", "/", 1, WorkStepType.Parallel, "cr");
            _workflowRepository.Add("/feedback/review", "/feedback", 1, WorkStepType.Normal, "cr");
            _workflowRepository.Add("/feedback/test", "/feedback", 2, WorkStepType.Normal, "cr");
            Assert.AreEqual(2, _workflowRepository.GetChildWorkSteps("/feedback").Count());
        }

        [TestMethod]
        public void ShouldFindSingleWorkItemAddedToAStep()
        {
            _workflowRepository.Add("/development", "/", 1, WorkStepType.Begin, "cr");
            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));

            WorkItem item = _workItemRepository.GetWorkItem("cr1");

            Assert.AreEqual("cr1", item.Id);
            Assert.IsNull(null, item.ParentId);
            Assert.AreEqual("/development", item.Path);
            Assert.AreEqual(WorkItemStatus.Normal, item.Status);
        }

        [TestMethod]
        public void ShouldNotCreateWorkItemInParallelStep()
        {
            CreateSimpleParallelWorkflow();
            AssertUtils.AssertThrows<InvalidOperationException>(
                () => _wp.CreateWorkItem(WorkItem.New("cr1","/feedback")));
        }

        [TestMethod]
        public void ShoudSplitWorkItem()
        {
            CreateSimpleParallelWorkflow();

            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
            WorkItem item = _workItemRepository.GetWorkItem("cr1");

            var parallelStepHelper = new ParallelStepHelper(_workflowRepository);

            WorkStep feedbackStep = _workflowRepository.GetWorkStep("/feedback");

            IEnumerable<WorkItem> newWorkItems = parallelStepHelper.SplitForParallelism(item, feedbackStep);

            WorkItem reviewWorkItem = newWorkItems.ElementAt(0);
            WorkItem testWorkItem = newWorkItems.ElementAt(1);

            Assert.IsNotNull(reviewWorkItem);
            Assert.AreEqual("/development", reviewWorkItem.Path);
            Assert.AreEqual("cr cr-review", reviewWorkItem.Classes.Join(' '));
            Assert.IsNotNull(testWorkItem);
            Assert.AreEqual("/development", testWorkItem.Path);
            Assert.AreEqual("cr cr-test", testWorkItem.Classes.Join(' '));
        }

        [TestMethod]
        public void ShouldLockWorkItemAndCreateChildWorkItemsWhenMovedToParallelStep()
        {
            CreateSimpleParallelWorkflow();

            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
            Assert.AreEqual(1, _wp.GetWorkItems("/development").Select(wi => wi.Id == "cr1").Count());

            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback/review"));

            Assert.AreEqual(1, _wp.GetWorkItems("/feedback/review").Where(wi => wi.Id == "cr1-review").Count());
            Assert.AreEqual(1, _wp.GetWorkItems("/development").Where(wi => wi.Id == "cr1-test").Count());
            Assert.AreEqual(0, _wp.GetWorkItems("/development").Where(wi => wi.Id == "cr1").Count());
        }

        [TestMethod]
        public void ShouldAlsoDeleteChildrenOfParalleledWorkItem()
        {
            CreateSimpleParallelWorkflow();

            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback/review"));

            Assert.IsTrue(_wp.ExistsWorkItem("cr1"));
            Assert.IsTrue(_wp.ExistsWorkItem("cr1-review"));
            Assert.IsTrue(_wp.ExistsWorkItem("cr1-test"));

            _wp.DeleteWorkItem("cr1");

            Assert.IsFalse(_wp.ExistsWorkItem("cr1"));
            Assert.IsFalse(_wp.ExistsWorkItem("cr1-review"));
            Assert.IsFalse(_wp.ExistsWorkItem("cr1-test"));
        }


        [TestMethod]
        public void ShouldNotListParallelLockedWorkItem()
        {
            CreateSimpleParallelWorkflow();

            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback/review"));

            Assert.AreEqual(0, _wp.GetWorkItems("/feedback").Where(wi => wi.Id == "cr1").Count());
        }

        [TestMethod]
        public void ShouldNotBeAbleToMoveParallelLockedWorkItem()
        {
            CreateSimpleParallelWorkflow();

            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback/review"));

            AssertUtils.AssertThrows<InvalidOperationException>(
                () => _wp.UpdateWorkItem(WorkItem.New("cr1", "/done")));
        }


        [TestMethod]
        public void ShouldLockWorkItemAndCreateChildWorkItemsWhenMovedToRootOfParallelStep()
        {
            CreateSimpleParallelWorkflow();

            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback"));

            Assert.AreEqual(1, _wp.GetWorkItems("/feedback/review").Where(wi => wi.Id == "cr1-review").Count());
            Assert.AreEqual(1, _wp.GetWorkItems("/development").Where(wi => wi.Id == "cr1-test").Count());
        }

        [TestMethod]
        public void ShouldMoveSecondChildItemWhenParentParallelized()
        {
            CreateSimpleParallelWorkflow();

            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback"));

            Assert.AreEqual(1, _wp.GetWorkItems("/feedback/review").Where(wi => wi.Id == "cr1-review").Count());
            Assert.AreEqual(1, _wp.GetWorkItems("/development").Where(wi => wi.Id == "cr1-test").Count());

            _wp.UpdateWorkItem(WorkItem.New("cr1-test", "/feedback/test"));
            Assert.AreEqual(1, _wp.GetWorkItems("/feedback/test").Where(wi => wi.Id == "cr1-test").Count());
        }

        [TestMethod]
        public void ShouldOnlyBeAbleToMoveChildItemToDedicatedParallelStep()
        {
            CreateSimpleParallelWorkflow();

            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback"));

            Assert.AreEqual(1, _wp.GetWorkItems("/feedback/review").Where(wi => wi.Id == "cr1-review").Count());
            Assert.AreEqual(1, _wp.GetWorkItems("/development").Where(wi => wi.Id == "cr1-test").Count());


            AssertUtils.AssertThrows<InvalidOperationException>(
                ()=> _wp.UpdateWorkItem(WorkItem.New("cr1-test", "/feedback/review")));

            Assert.AreEqual(1, _wp.GetWorkItems("/development").Where(wi => wi.Id == "cr1-test").Count());
            Assert.AreEqual(1, _wp.GetWorkItems("/feedback/review").Where(wi => wi.Id == "cr1-review").Count());
        }

        [TestMethod]
        public void ShouldNotBeAbleToDeleteChildOfParalleledWorkItem()
        {
            CreateSimpleParallelWorkflow();

            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback/review"));

            AssertUtils.AssertThrows<InvalidOperationException>(
                () =>
                _wp.DeleteWorkItem("cr1-test")
                );
        }

        [TestMethod]
        public void ShouldMergeChildItemsWhenMovedToSameStepOutsideParallelization()
        {
            CreateSimpleParallelWorkflow();

            _wp.CreateWorkItem(WorkItem.New("cr1","/development"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback"));

            Assert.AreEqual(1, _wp.GetWorkItems("/feedback/review").Where(wi => wi.Id == "cr1-review").Count());
            Assert.AreEqual(1, _wp.GetWorkItems("/development").Where(wi => wi.Id == "cr1-test").Count());

            _wp.UpdateWorkItem(WorkItem.New("cr1-test", "/done"));
            _wp.UpdateWorkItem(WorkItem.New("cr1-review", "/done"));
            Assert.AreEqual(1, _wp.GetWorkItems("/done").Where(wi => wi.Id == "cr1").Count());
            Assert.AreEqual(0, _wp.GetWorkItems("/done").Where(wi => wi.Id == "cr1-review").Count());
            Assert.AreEqual(0, _wp.GetWorkItems("/done").Where(wi => wi.Id == "cr1-test").Count());
        }

        [TestMethod]
        public void ShouldMergeChildItemsWhenMovedToSameStepOutsideParallelizationAndChildWorkItemWasCreatedInExpandStep ()
        {
            CreateParallelWorkflowWithExpandStep();

            _wp.CreateWorkItem(WorkItem.New("cr1","/scheduled"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/development"));
            _wp.CreateWorkItem(WorkItem.New("cr1-1","/development/inprocess/cr1/tasks"));
            _wp.UpdateWorkItem(WorkItem.New("cr1-1", "/development/inprocess/cr1/tasks/done"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/development/done"));

            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback"));
            _wp.UpdateWorkItem(WorkItem.New("cr1-test", "/feedback/test"));

            Assert.AreEqual(1, _wp.GetWorkItems("/feedback/review").Where(wi => wi.Id == "cr1-review").Count());
            Assert.AreEqual(1, _wp.GetWorkItems("/feedback/test").Where(wi => wi.Id == "cr1-test").Count());

            _wp.UpdateWorkItem(WorkItem.New("cr1-test", "/done"));
            _wp.UpdateWorkItem(WorkItem.New("cr1-review", "/done"));
            Assert.AreEqual(1, _wp.GetWorkItems("/done").Where(wi => wi.Id == "cr1").Count());
            Assert.AreEqual(0, _wp.GetWorkItems("/done").Where(wi => wi.Id == "cr1-review").Count());
            Assert.AreEqual(0, _wp.GetWorkItems("/done").Where(wi => wi.Id == "cr1-test").Count());
        }

        [TestMethod]
        public void ShouldNotBeAbleToMoveFromExpandStepToParallelStep()
        {
            CreateParallelWorkflowWithExpandStep();

            _wp.CreateWorkItem(WorkItem.New("cr1","/scheduled"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/development"));

            AssertUtils.AssertThrows<InvalidOperationException>(
                () => _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback")));
        }

        //[TestMethod, Ignore]
        //public void ShouldBeAbleToMoveFromTransientStepToParallelStepAndCreateNewTransientStepForParallelledSibling()
        //{
        //    CreateParallelWorkflowWithExpandStep();

        //    _wp.CreateWorkItem("cr1", "/scheduled");
        //    _wp.UpdateWorkItem("cr1", "/development");
        //    Assert.IsTrue(_wp.ExistsWorkStep("/development/inprocess/cr1"));
            
        //    _wp.UpdateWorkItem("cr1", "/feedback/review");

        //    Assert.IsFalse(_wp.ExistsWorkStep("/development/inprocess/cr1"));
        //    Assert.IsTrue(_wp.ExistsWorkStep("/development/inprocess/cr1-test"));

        //    Assert.AreEqual("/development/inprocess/cr1-test", _wp.GetWorkItem("cr1-test").Path);
        //    Assert.AreEqual("/feedback/review", _wp.GetWorkItem("cr1-review").Path);

        //}

        [TestMethod]
        public void ShouldBeAbleToMoveParalleledWorkItemToExpandStep()
        {
            CreateParallelWorkflowWithExpandStep();

            _wp.CreateWorkItem(WorkItem.New("cr1","/scheduled"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback/review"));

            _wp.UpdateWorkItem(WorkItem.New("cr1-review", "/development/inprocess"));

            var workItem = _wp.GetWorkItem("cr1-review");
            Assert.AreEqual("/development/inprocess", workItem.Path);
        }

        [TestMethod]
        public void ShouldRemoveTransientStepWhenChildrenOfParallelledWorkItemAreMerged()
        {
            CreateParallelWorkflowWithExpandStep();

            _wp.CreateWorkItem(WorkItem.New("cr1","/scheduled"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback/review"));

            _wp.UpdateWorkItem(WorkItem.New("cr1-review", "/development/inprocess"));
            Assert.IsTrue(_wp.ExistsWorkStep("/development/inprocess/cr1-review"));

            _wp.UpdateWorkItem(WorkItem.New("cr1-review", "/scheduled"));
            Assert.IsFalse(_wp.ExistsWorkStep("/development/inprocess/cr1-review"));
        }

        [TestMethod]
        public void ShouldNotBeAbleToMoveParallelledWorkItemToTransientStepOfSibling()
        {
            CreateParallelWorkflowWithExpandStep();

            _wp.CreateWorkItem(WorkItem.New("cr1","/scheduled"));
            _wp.UpdateWorkItem(WorkItem.New("cr1", "/feedback/review"));

            _wp.UpdateWorkItem(WorkItem.New("cr1-review", "/development/inprocess"));

            AssertUtils.AssertThrows<InvalidOperationException>(
            () => _wp.UpdateWorkItem(WorkItem.New("cr1-test", "/development/inprocess/cr1-review")));
        }

        private void CreateSimpleParallelWorkflow()
        {
            _workflowRepository.Add("/development", "/", 1, WorkStepType.Begin, "cr");
            _workflowRepository.Add("/feedback", "/", 2, WorkStepType.Parallel, "cr");
            _workflowRepository.Add("/feedback/review", "/feedback", 1, WorkStepType.Normal, "cr-review");
            _workflowRepository.Add("/feedback/test", "/feedback", 2, WorkStepType.Normal, "cr-test");
            _workflowRepository.Add("/done", "/", 2, WorkStepType.End, "cr");
        }

        private void CreateParallelWorkflowWithExpandStep()
        {
            _workflowRepository.Add("/scheduled", "/", 1, WorkStepType.Begin, "cr", "Scheduled");
            _workflowRepository.Add("/analysis", "/", 1, WorkStepType.Normal, "cr", "Analysis");
            _workflowRepository.Add("/analysis/inprocess", "/analysis", 1, WorkStepType.Normal, "cr");
            _workflowRepository.Add("/analysis/done", "/analysis", 1, WorkStepType.Normal, "cr");
            _workflowRepository.Add("/development", "/", 2, WorkStepType.Begin, "cr", "Development");
            _workflowRepository.Add("/development/inprocess", "/development", 1, WorkStepType.Expand, "cr");
            _workflowRepository.Add("/development/inprocess/tasks", "/development/inprocess", 1, WorkStepType.Normal,
                                    "task", "Tasks");
            _workflowRepository.Add("/development/inprocess/tasks/new", "/development/inprocess/tasks", 1,
                                    WorkStepType.Begin, "task");
            _workflowRepository.Add("/development/inprocess/tasks/inprocess", "/development/inprocess/tasks", 1,
                                    WorkStepType.Normal, "task");
            _workflowRepository.Add("/development/inprocess/tasks/done", "/development/inprocess/tasks", 1,
                                    WorkStepType.End, "task");
            _workflowRepository.Add("/development/done", "/development", 2, WorkStepType.End, "cr");
            _workflowRepository.Add("/feedback", "/", 3, WorkStepType.Parallel, "cr");
            _workflowRepository.Add("/feedback/review", "/feedback", 1, WorkStepType.Normal, "cr-review", "Review");
            _workflowRepository.Add("/feedback/test", "/feedback", 2, WorkStepType.Normal, "cr-test", "Test");
            _workflowRepository.Add("/done", "/", 4, WorkStepType.End, "cr", "Done");
        }
    }
}   