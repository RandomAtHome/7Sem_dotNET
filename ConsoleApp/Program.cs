using System;
using System.Threading;

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
            var loopThread = new Thread(printLoop)
            {
                IsBackground = true
            };
            loopThread.Start(parallelRecognition);
            Console.ReadLine();
            Console.WriteLine("Received interrupt!");
            parallelRecognition.Stop();
            loopThread.Join();
            Console.WriteLine("Printed all queue");
        }

        static void printLoop(object data)
        {
            var parallelRecognition = data as ParallelRecognition.ParallelRecognition;
            while (!parallelRecognition.HasFinished && parallelRecognition.CreationTimes.Count != 0)
            {
                while (parallelRecognition.CreationTimes.TryDequeue(out ParallelRecognition.ImageClassified item))
                {
                    Console.WriteLine(item);
                }
                Thread.Sleep(500);
            }
        }
    }
}
