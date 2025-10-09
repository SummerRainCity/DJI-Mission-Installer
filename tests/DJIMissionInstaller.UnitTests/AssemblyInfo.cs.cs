// WPF and file system watcher tests are sensitive to parallel execution.
// Disable test parallelization at the assembly level to avoid flakiness and
// hard-to-troubleshoot cross-thread issues with WPF's Dispatcher.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
