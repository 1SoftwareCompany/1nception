﻿using Machine.Specifications;
using System;
using System.Collections.Generic;

namespace One.Inception.Tests.Middleware;

[Subject("One.Inception.Middleware")]
public class When_attaching_multiple_middlewares
{
    Establish context = () =>
    {
        executionChain = new TestExecutionChain();
        expectedExecution = new List<ExecutionToken>();
        var firstToken = executionChain.CreateToken();
        mainMiddleware = new TestMiddleware(firstToken);
        expectedExecution.Add(firstToken);
        for (int i = 0; i < new Random().Next(5, 20); i++)
        {
            var nextToken = executionChain.CreateToken();
            var nextMiddleware = new TestMiddleware(nextToken);
            expectedExecution.Add(nextToken);
            mainMiddleware.Use(nextMiddleware);
        }
    };

    Because of = async () => await mainMiddleware.RunAsync(invocationContext).ConfigureAwait(false);

    It the_execution_chain_should_not_be_empty = () => executionChain.GetTokens().ShouldNotBeEmpty();

    It should_have_multiple_execution_tokens = () => executionChain.GetTokens().Count.ShouldEqual(expectedExecution.Count);

    It should_have_the_expected_execution = () => executionChain.ShouldMatch(expectedExecution);


    static TestMiddleware mainMiddleware;

    static TestExecutionChain executionChain;

    static List<ExecutionToken> expectedExecution;

    static string invocationContext = "Test context";

}
