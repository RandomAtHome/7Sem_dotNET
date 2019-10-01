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
        private static readonly object synchronizationObject = new object();
        ManualResetEvent hasFinishedEvent = new ManualResetEvent(true);
        ManualResetEvent areFreeWorkersEvent = new ManualResetEvent(false);

        Dictionary<string, DateTime> creationTimes = new Dictionary<string, DateTime>();
        private string directoryPath;
        private bool hasFinished = true;

        bool IsInterrupted { get; set; }
        public bool HasFinished { get => hasFinished; private set => hasFinished = value; }
        public string DirectoryPath { get => directoryPath; private set => directoryPath = value; }
        public Dictionary<string, DateTime> CreationTimes { get => creationTimes;}

        public ParallelRecognition(string directoryPath)
        {
            DirectoryPath = directoryPath;    
        }

        public bool Run()
        {
            try
            {
                new Thread(ManageJobs)
                {
                    IsBackground = true
                }.Start(Directory.GetFiles(DirectoryPath));
            } catch (ArgumentException) {
                return false;
            }      
            return true;
        }

        void ManageJobs(object filenames)
        {
            hasFinishedEvent.Reset();
            HasFinished = false;
            IsInterrupted = false;
            ThreadPool.GetMaxThreads(out int workerThreadsCount, out int portThreads);
            var workers = new Thread[workerThreadsCount];
            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = new Thread(RecognizeContentsStub)
                {
                    IsBackground = true
                };
            }

            var files = filenames as string[];
            int fileIndex = 0;
            while (fileIndex < files.Length)
            {
                if (IsInterrupted) break;
                areFreeWorkersEvent.Reset();
                for (int i = 0; i < workers.Length; i++)
                {
                    if (!workers[i].IsAlive && !IsInterrupted && fileIndex < files.Length)
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
                if (worker.IsAlive) worker.Join();
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
            lock (synchronizationObject)
            {
                // Here be onnxruntime action!
                CreationTimes[filePath] = File.GetCreationTime(filePath);
            }
            areFreeWorkersEvent.Set();
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
                        lock (synchronizationObject)
                        {
                            // Here be onnxruntime action!
                        }
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
