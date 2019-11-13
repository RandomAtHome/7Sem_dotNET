//variant 2a
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics.Tensors;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace ParallelRecognition
{
    public class ParallelRecognition
    {
        readonly ManualResetEvent hasFinishedEvent = new ManualResetEvent(true);

        bool IsInterrupted { get; set; } = false;
        public bool HasFinished { get; private set; } = true;
        public string DirectoryPath { get; private set; }
        public ConcurrentQueue<ImageClassified> CreationTimes { get; } = new ConcurrentQueue<ImageClassified>();

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
                    if (FindInDB(tensor) is ImageClassified imageClassified)
                    {
                        imageClassified.ImagePath = filePath;
                        CreationTimes.Enqueue(imageClassified);
                        continue;
                    }
                    foreach (var name in inputMeta.Keys)
                    {
                        container.Add(NamedOnnxValue.CreateFromTensor<float>(name, tensor));
                    }
                    if (IsInterrupted) break;
                    using (var results = session.Run(container))
                    {
                        foreach (var r in results)
                        {
                            double[] exp = new double[r.AsEnumerable<float>().Count()];
                            int i = 0;
                            foreach (var val in r.AsEnumerable<float>())
                            {
                                exp[i++] = Math.Exp(val);
                            }
                            var softmax = exp.Select(j => j / exp.Sum()).ToArray();
                            var max_val1 = softmax.Max();
                            var max_ind1 = Array.IndexOf(softmax, max_val1);
                            using (var db = new RecognitionModelContainer())
                            {
                                var blob = new Blobs()
                                {
                                    FileContent = ObjectToByteArray(tensor)
                                };
                                var result = new Results()
                                {
                                    ClassId = max_ind1,
                                    Probability = max_val1,
                                    FileHash = GetTensorHash(tensor),
                                    Blob = blob,
                                };
                                blob.Result = result;
                                db.Blobs.Add(blob);
                                db.Results.Add(result);
                                db.SaveChanges();
                            }
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

        private ImageClassified FindInDB(DenseTensor<float> tensor)
        {
            var tensorHash = GetTensorHash(tensor);
            using (var db = new RecognitionModelContainer())
            {
                var query = from recognitionResult in db.Results
                         where recognitionResult.FileHash == tensorHash
                         select recognitionResult;
                foreach (var result in query)
                {
                    var tensorBytes = ObjectToByteArray(tensor);
                    if (result.Blob.FileContent.Length == tensorBytes.Length && result.Blob.FileContent.SequenceEqual(tensorBytes))
                    {
                        return new ImageClassified()
                        {
                            ImagePath = "",
                            ClassName = result.ClassId.ToString(), // here be translation to real class name later
                            Certainty = result.Probability,
                        };
                    }
                }
            }
            return null;
        }

        private byte[] ObjectToByteArray(object obj)
        {
            if (obj == null) return null;
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, (obj as DenseTensor<float>).ToArray());
                bytes = ms.ToArray();
            }
            return bytes;
        }

        static Int64 GetTensorHash(DenseTensor<float> tensor)
        {
            Int64 result = 0;
            foreach (var v in tensor)
            {
                result += BitConverter.ToInt32(BitConverter.GetBytes(v), 0);
            }
            return result;
        }

        static DenseTensor<float> LoadTensorFromFile(string filename)
        {
            Bitmap tmp = new Bitmap(filename);
            Bitmap img = new Bitmap(tmp, new Size(224, 224));
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
            tmp.Dispose();
            img.Dispose();
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
