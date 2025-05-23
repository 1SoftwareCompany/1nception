﻿using One.Inception.Workflow;
using Machine.Specifications;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace One.Inception.Tests.Middleware;

[Subject("One.Inception.Middleware")]
public class When_transfering_to_another_middleware_and_there_is_no_followup_middleware
{
    Establish context = () =>
    {
        expectedExecution = new List<ExecutionToken>();
        executionChain = new TestExecutionChain();
        var mainToken = executionChain.CreateToken();
        mainMiddleware = new TestMiddleware(mainToken);
        expectedExecution.Add(mainToken);


        var secondToken = executionChain.CreateToken();
        var secondMiddleware = new TestMiddleware(secondToken);
        mainMiddleware.Use((execution) =>
        {
            execution.Transfer(secondMiddleware);
            return Task.CompletedTask;
        });
        expectedExecution.Add(secondToken);


    };

    Because of = async () => await mainMiddleware.RunAsync(invocationContext).ConfigureAwait(false);

    It the_execution_chain_should_not_be_empty = () => executionChain.GetTokens().ShouldNotBeEmpty();

    It should_have_the_expected_execution = () => executionChain.ShouldMatch(expectedExecution);

    static TestMiddleware mainMiddleware;

    static TestExecutionChain executionChain;

    static List<ExecutionToken> expectedExecution;

    static string invocationContext = "Test context";

}
