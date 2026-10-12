using Xunit;

// Les tests manipulent des fichiers SQLite, le système de fichiers temporaire et des singletons applicatifs.
// La désactivation de la parallélisation inter-classes garantit une isolation parfaite sans interférence de threads.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
