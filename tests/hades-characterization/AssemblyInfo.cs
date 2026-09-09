using Xunit;

// The server opens a hardcoded object-server port (2620), so only one isolated instance can run at a
// time. Without this, xunit runs the test classes in parallel and their servers collide.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
