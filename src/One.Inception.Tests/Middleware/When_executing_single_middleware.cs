﻿using Machine.Specifications;
using System.Linq;

namespace One.Inception.Tests.Middleware;

[Subject("One.Inception.Middleware")]
public class When_executing_single_middleware
{
    Establish context = () =>
    {
        executionChain = new TestExecutionChain();
        executionToken = executionChain.CreateToken();
        mainMiddleware = new TestMiddleware(executionToken);
    };

    Because of = async () => await mainMiddleware.RunAsync(invocationContext).ConfigureAwait(false);

    It the_execution_chain_should_not_be_empty = () =>

    executionChain.GetTokens().ShouldNotBeEmpty();

    It should_have_executed_only_once = () => executionChain.GetTokens().SingleOrDefault().ShouldNotBeNull();

    It should_have_notified_the_first_token = () => executionChain.GetTokens().SingleOrDefault().ShouldEqual(executionToken);


    static TestMiddleware mainMiddleware;

    static TestExecutionChain executionChain;

    static ExecutionToken executionToken;

    static string invocationContext = "Test context";

}
