using System;
using System.Collections.Generic;
using System.Text;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using Xunit;
using FluentAssertions;
using System.Linq;
using System.Threading.Tasks;
using WorkflowCore.Testing;

namespace WorkflowCore.IntegrationTests.Scenarios
{
    public class MultistepCompensationScenario2 : WorkflowTest<MultistepCompensationScenario2.Workflow, MultistepCompensationScenario2.Data>
    {
        public class Data
        {
            public string Id { get; set; }
        }

        public class PrepareDataStep : StepBody
        {
            public string Id { get; private set; }
            public int StepId { get; set; }

            public override ExecutionResult Run(IStepExecutionContext context)
            {
                Id = Guid.NewGuid().ToString();
                switch (StepId)
                {
                    case 1:
                    {
                        Workflow.ExpectedCompensationData1 = Id;
                        break;
                    }
                    case 2:
                    {
                        Workflow.ExpectedCompensationData2 = Id;
                        break;
                    }
                }
                
                return ExecutionResult.Next();
            }
        }
        
        public class Workflow : IWorkflow<Data>
        {
            public static string CompensationData1;
            public static string CompensationData2;

            public static string ExpectedCompensationData1;
            public static string ExpectedCompensationData2;

            public string Id => "CompensatingWorkflow";
            public int Version => 1;
            public void Build(IWorkflowBuilder<Data> builder)
            {
                builder
                    .StartWith(context => ExecutionResult.Next())
                    .Saga(x => x
                        .StartWith(context => ExecutionResult.Next())
                        .CompensateWithSequence(context =>
                            context.StartWith<PrepareDataStep>()
                                .Input(step => step.StepId, data => 1)
                                .Output(data => data.Id, step => step.Id)
                                .Then(then =>
                                {
                                    CompensationData1 = ((Data) then.Workflow.Data).Id;
                                }))
                        .Then(context => ExecutionResult.Next())
                        .CompensateWithSequence(context =>
                            context.StartWith<PrepareDataStep>()
                                .Input(step => step.StepId, data => 2)
                                .Output(data => data.Id, step => step.Id)
                                .Then(then =>
                                {
                                    CompensationData2 = ((Data) then.Workflow.Data).Id;
                                }))
                        .Then(context => throw new Exception()));
            }
        }

        public MultistepCompensationScenario2()
        {
            Setup();
        }
        
        [Fact]
        public void MultiCompensationStepOrder()
        {
            var workflowId = StartWorkflow(null);
            WaitForWorkflowToComplete(workflowId, TimeSpan.FromSeconds(30));

            GetStatus(workflowId).Should().Be(WorkflowStatus.Complete);

            Workflow.CompensationData1.Should().Be(Workflow.ExpectedCompensationData1);
            Workflow.CompensationData2.Should().Be(Workflow.ExpectedCompensationData2);
            Workflow.CompensationData2.Should().NotBe(Workflow.CompensationData1);
        }
    }
}
