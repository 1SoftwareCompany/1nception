# 1nception is a lightweight framework for building event driven systems with DDD/CQRS in mind.

HardFork
========
1nception is a hard fork from the Cronus framework. At the time of the fork Cronus was v11. 1nception will have its own versioning. In order to avoid conflicts all namespaces will be renamed and a separate nuget package is published `1nception`

## Handler cancellation (v14)

See [handler cancellation and graceful shutdown](docs/cancellation.md) for the
breaking handler changes, cancellation-source ownership, shutdown configuration,
and message redelivery semantics.