using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var dirPath = Console.ReadLine();
            ParallelRecognition.ParallelRecognition parallelRecognition = new ParallelRecognition.ParallelRecognition(dirPath);
            parallelRecognition.Run();
        }
    }
}
