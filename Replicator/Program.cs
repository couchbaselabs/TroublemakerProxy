using System;
using System.Threading;

using Couchbase.Lite;
using Couchbase.Lite.Sync;

namespace Replication
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Press any key to start...");
            Console.ReadKey();
            using (var db = new Database("db")) {
                var config = new ReplicatorConfiguration(db, new URLEndpoint(new Uri("ws://localhost:5984/db")))
                    { Continuous = true };
                var repl = new Replicator(config);
                repl.Start();

                var key = new ConsoleKeyInfo();
                var r = new Random();
                do {
                    Console.WriteLine("Press A to add document, Press E to end");
                    key = Console.ReadKey();
                    if (key.Key == ConsoleKey.A) {
                        using (var doc = new MutableDocument()) {
                            doc.SetInt("rand", r.Next());
                            db.Save(doc);
                        }
                    }
                } while (key.Key != ConsoleKey.E);

                repl.Stop();
                while (repl.Status.Activity != ReplicatorActivityLevel.Stopped) {
                    Console.WriteLine("Waiting for replicator to stop...");
                    Thread.Sleep(2000);
                }
            }
        }
    }
}
