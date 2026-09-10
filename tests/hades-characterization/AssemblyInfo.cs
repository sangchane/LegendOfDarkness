using Xunit;

// The object server's port used to be written into the server's code, so two isolated instances collided
// and the suite had to run one class at a time. Each run brings its own port now, so the classes run
// together — which is most of the difference between three minutes and one.
[assembly: CollectionBehavior(DisableTestParallelization = false)]
