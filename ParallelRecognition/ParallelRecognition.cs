//variant 2a
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics.Tensors;
using System.Threading;

namespace ParallelRecognition
{
    public class ParallelRecognition
    {
        ManualResetEvent hasFinishedEvent = new ManualResetEvent(true);

        ConcurrentQueue<ImageClassified> creationTimes = new ConcurrentQueue<ImageClassified>();
        private string directoryPath;
        private volatile bool hasFinished = true;
        private volatile bool isInterrupted = false;

        bool IsInterrupted { get => isInterrupted; set => isInterrupted = value; }
        public bool HasFinished { get => hasFinished; private set => hasFinished = value; }
        public string DirectoryPath { get => directoryPath; private set => directoryPath = value; }
        public ConcurrentQueue<ImageClassified> CreationTimes { get => creationTimes; }

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
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }

        void ManageJobs(object filenames)
        {
            hasFinishedEvent.Reset();
            HasFinished = false;
            IsInterrupted = false;
            var queue = new ConcurrentQueue<string>(filenames as string[]);
            int workerThreadsCount = Environment.ProcessorCount;
            var workers = new Thread[workerThreadsCount];
            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = new Thread(RecognizeContents)
                {
                    IsBackground = true
                };
                workers[i].Start(queue);
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

        void RecognizeContents(object obj)
        {
            var queue = obj as ConcurrentQueue<string>;
            using (var session = new InferenceSession(@"DnnImageModels\ResNet50Onnx\resnet50v2.onnx"))
            {
                while (queue.TryDequeue(out string filePath))
                {
                    var inputMeta = session.InputMetadata;
                    var container = new List<NamedOnnxValue>();
                    if (IsInterrupted) break;
                    var tensor = LoadTensorFromFile(filePath);
                    foreach (var name in inputMeta.Keys)
                    {
                        container.Add(NamedOnnxValue.CreateFromTensor<float>(name, tensor));
                    }
                    if (IsInterrupted) break;
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
                            CreationTimes.Enqueue(new ImageClassified()
                            {
                                ImagePath = filePath,
                                ClassName = max_ind1.ToString(),
                                Certainty = max_val1,
                            });
                        }
                    }
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

    public class ImageClassified
    {
        public string ImagePath { get; set; }
        public string ClassName { get; set; }
        public double Certainty { get; set; }
    }
}
