using System;
//using System.Threading;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            //var dirPath = Console.ReadLine();
            var dirPath = @"D:\Coding\Task1_ParallelRecognition\ILSVRC2012_img_val";
            var parallelRecognition = new ParallelRecognition.ParallelRecognition(dirPath);
            if (!parallelRecognition.Run())
            {
                Console.WriteLine("Bad directory input!");
                Console.ReadLine();
                return;
            }
            //Thread.Sleep(500);
            Console.ReadLine();
            Console.WriteLine("Received interrupt!");
            parallelRecognition.Stop();
            foreach (var pair in parallelRecognition.CreationTimes)
            {
                Console.WriteLine(pair.Key.ToString() + " | " + pair.Value.ToString());
            }
        }
    }
}
