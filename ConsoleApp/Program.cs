using ParallelRecognition1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var dirPath = Console.ReadLine();
            ParallelRecognition parallelRecognition = new ParallelRecognition(dirPath);
            parallelRecognition.Run();
        }
    }
}
