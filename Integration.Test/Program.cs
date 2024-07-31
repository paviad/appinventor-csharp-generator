using Testa;

namespace Integration.Test;

internal class Program {
    private static void Main(string[] args) {
        var h = new HelloPurr();
        h.MAIN();
        h.Testa();
        Console.WriteLine("Hello, World!");
    }
}
