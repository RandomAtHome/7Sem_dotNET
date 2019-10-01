//variant a2
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics.Tensors;
using System.Threading;
using Microsoft.ML.OnnxRuntime;

namespace ParallelRecognition
{
    public class ParallelRecognition
    {
        ManualResetEvent hasFinishedEvent = new ManualResetEvent(true);
        ManualResetEvent areFreeWorkersEvent = new ManualResetEvent(false);
        AutoResetEvent isDictFreeEvent = new AutoResetEvent(true);
        Dictionary<string, DateTime> creationTimes = new Dictionary<string, DateTime>();

        private string directoryPath;
        public string DirectoryPath { get => directoryPath; private set => directoryPath = value; }

        private bool hasFinished = true;
        public bool HasFinished { get => hasFinished; private set => hasFinished = value; }
        bool IsInterrupted { get; set; }
        Thread manager_thread = null;

        public ParallelRecognition(string directoryPath)
        {
            DirectoryPath = directoryPath;
            manager_thread = new Thread(ManageJobs);
        }

        public bool Run()
        {
            HasFinished = false;
            IsInterrupted = false;
            manager_thread.Start(Directory.GetFiles(DirectoryPath));
            return true;
        }

        void ManageJobs(object filenames)
        {
            var files = filenames as string[];
            int fileIndex = 0;
            ThreadPool.GetMaxThreads(out int workerThreadsCount, out int portThreads);
            var workers = new Thread[workerThreadsCount];
            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = new Thread(RecognizeContentsStub)
                {
                    IsBackground = true
                };
            }
            while (fileIndex < files.Length)
            {
                areFreeWorkersEvent.Reset();
                if (IsInterrupted) break;
                for (int i = 0; i < workers.Length; i++)
                {
                    if (!workers[i].IsAlive && !IsInterrupted)
                    {
                        workers[i] = new Thread(RecognizeContentsStub)
                        {
                            IsBackground = true
                        };
                        workers[i].Start(files[fileIndex++]);
                    }
                }
                areFreeWorkersEvent.WaitOne();
            }
            foreach (var worker in workers)
            {
                worker.Join();
            }
            HasFinished = true;
            IsInterrupted = false;
            hasFinishedEvent.Set();
        }

        public bool Stop()
        {
            IsInterrupted = !HasFinished;
            hasFinishedEvent.WaitOne();
            return true;
        }

        void RecognizeContentsStub(object obj)
        {
            var filePath = obj as string;
            isDictFreeEvent.WaitOne();
            creationTimes[filePath] = File.GetCreationTime(filePath);
            isDictFreeEvent.Set();
            //return "{'Yay': 0}";
        }

        static void RecognizeContents(string filePath)
        {
            string modelPath = @"DnnImageModels\ResNet50Onnx\resnet50v2.onnx";
            using (var session = new InferenceSession(modelPath))
            {
                var inputMeta = session.InputMetadata;
                var container = new List<NamedOnnxValue>();

                float[] inputData = LoadTensorFromFile(filePath); // this is the data for only one input tensor for this model

                foreach (var name in inputMeta.Keys)
                {
                    var tensor = new DenseTensor<float>(inputData, inputMeta[name].Dimensions);
                    container.Add(NamedOnnxValue.CreateFromTensor<float>(name, tensor));
                }

                // Run the inference
                using (var results = session.Run(container))  // results is an IDisposableReadOnlyCollection<DisposableNamedOnnxValue> container
                {
                    // dump the results
                    foreach (var r in results)
                    {
                        Console.WriteLine("Output for {0}", r.Name);
                        Console.WriteLine(r.AsTensor<float>().GetArrayString());
                    }
                }
            }
        }

        static float[] LoadTensorFromFile(string filename)
        {
            var tensorData = new List<float>();

            //// read data from file
            //using (var inputFile = new System.IO.StreamReader(filename))
            //{
            //    inputFile.ReadLine(); //skip the input name
            //    string[] dataStr = inputFile.ReadLine().Split(new char[] { ',', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
            //    for (int i = 0; i < dataStr.Length; i++)
            //    {
            //        tensorData.Add(Single.Parse(dataStr[i]));
            //    }
            //}

            return tensorData.ToArray();
        }

    }
}
