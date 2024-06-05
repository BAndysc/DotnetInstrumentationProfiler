using DotnetProfiler;

namespace DemoApp;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Profiler.Enable();
        Thread thread = new Thread(MemoryIntensiveTask);
        thread.Start();
        LongFunc();
        thread.Join();
        Profiler.Disable();
        Profiler.SaveTrace("trace.bin");
    }

    public static void LongFunc()
    {
        Profiler.Start("Long func"u8);
        var bigarray = new byte[1000000];
        Thread.Sleep(5000);
        var bigarray2 = new byte[1000000];
        Profiler.Stop();
        working = false;
        Console.WriteLine("Done");
    }

    private static volatile bool working = true;

    static void MemoryIntensiveTask()
    {
        List<Node> nodes = new List<Node>();

        // Create a large number of nodes
        for (int i = 0; i < 100000; i++)
        {
            Node node = new Node(i);
            nodes.Add(node);
        }

        Console.WriteLine("Created");

        // Create complex references between nodes
        Random rand = new Random();
        for (var index = 0; index < nodes.Count && working; index++)
        {
            var node = nodes[index];
            for (int i = 0; i < 100; i++)
            {
                int targetIndex = rand.Next(nodes.Count);
                node.OutgoingReferences.Add(nodes[targetIndex]);
                nodes[targetIndex].IncomingReferences.Add(node);
            }
        }

        Console.WriteLine("connected");

        // Simulate workload
        while(working)
        {
            Console.WriteLine("Work work wrok");
            for (var index = 0; index < nodes.Count && working; index++)
            {
                var node = nodes[index];
                node.DoWork();
            }
        }

        // Clear references to induce GC pressure
        nodes.Clear();
        nodes = null;

        Console.WriteLine("GC");

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Console.WriteLine("Garbage collection forced.");
    }
}

class Node
{
    public int Id { get; private set; }
    public List<Node> IncomingReferences { get; private set; }
    public List<Node> OutgoingReferences { get; private set; }

    public Node(int id)
    {
        Id = id;
        IncomingReferences = new List<Node>();
        OutgoingReferences = new List<Node>();
    }

    public void DoWork()
    {
        // Simulate some work that involves traversing the graph
        int sum = 0;
        foreach (var node in OutgoingReferences)
        {
            sum += node.Id;
        }
    }
}