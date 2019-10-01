//variant a2
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        InferenceSession session = null;
        
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
            session = new InferenceSession(@"DnnImageModels\ResNet50Onnx\resnet50v2.onnx");
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
                workers[i] = new Thread(RecognizeContents)
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
                        workers[i] = new Thread(RecognizeContents)
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

        void RecognizeContents(object obj)
        {
            var filePath = obj as string;
            var inputMeta = session.InputMetadata;
            var container = new List<NamedOnnxValue>();

            var tensor = LoadTensorFromFile(filePath);
            foreach (var name in inputMeta.Keys)
            {
                container.Add(NamedOnnxValue.CreateFromTensor<float>(name, tensor));
            }

            using (var results = session.Run(container)) 
            {
                foreach (var r in results)
                {
                    var tmp = r.AsEnumerable<float>().ToArray();
                    double[] exp = new double[tmp.Length];
                    int i = 0;
                    foreach (var x in tmp)
                    {
                        exp[i++] = Math.Exp((double)x);
                    }
                    var sum_exp = exp.Sum();
                    var softmax = exp.Select(j => j / sum_exp);
                    double[] sorted = new double[tmp.Length];
                    Array.Copy(softmax.ToArray(), sorted, tmp.Length);
                    Array.Sort(sorted, (x, y) => -x.CompareTo(y));
                    var max_val1 = sorted[0];
                    var max_ind1 = softmax.ToList().IndexOf(max_val1);
                    Console.WriteLine("[" + filePath + "] 1) '" + max_ind1 + "' " + Math.Round(max_val1, 2) * 100 + "%");
                }
            }
        }

        static DenseTensor<float> LoadTensorFromFile(string filename)
        {
            Bitmap img = new Bitmap(filename);
            img = new Bitmap(img, new Size(224, 224));
            float[,,,] data = new float[1, 3, img.Height, img.Width];
            float[] mean = new float[3] { 0.485F, 0.456F, 0.406F };
            float[] std = new float[3] { 0.229F, 0.224F, 0.224F };
            for (int i = 0; i < img.Width; i++)
            {
                for (int j = 0; j < img.Height; j++)
                {
                    data[0, 0, j, i] = ((float)img.GetPixel(i, j).R / 255 - mean[0]) / std[0];
                    data[0, 1, j, i] = ((float)img.GetPixel(i, j).G / 255 - mean[1]) / std[1];
                    data[0, 2, j, i] = ((float)img.GetPixel(i, j).B / 255 - mean[2]) / std[2];
                }
            }
            return data.ToTensor<float>();
        }

    }
}
