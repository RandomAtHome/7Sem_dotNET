﻿//variant 4
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace CoreParallelRecognition
{
    public class ParallelRecognition
    {
        static readonly ManualResetEvent limitHit = new ManualResetEvent(true);
        static private readonly int threadLimit = Environment.ProcessorCount;
        static private volatile int curThreads = 0;

        static public ImageClassified RecognizeContents(byte[] fileBytes, string filename)
        {
            if (FindInDB(fileBytes) is ImageClassified imageClassified)
            {
                imageClassified.ImagePath = filename;
                return imageClassified;
            }
            curThreads++;
            if (curThreads > threadLimit)
            {
                limitHit.Reset();
                limitHit.WaitOne();
            }
            using var session = new InferenceSession(@"C:\Users\randomnb\Desktop\DnnImageModels\ResNet50Onnx\resnet50v2.onnx");
            var inputMeta = session.InputMetadata;
            var container = new List<NamedOnnxValue>();
            var tensor = LoadTensorFromFileBytes(fileBytes);
            foreach (var name in inputMeta.Keys)
            {
                container.Add(NamedOnnxValue.CreateFromTensor<float>(name, tensor));
            }
            using var results = session.Run(container);
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
                var tensorHash = GetTensorHash(fileBytes);
                using (var db = new RecognitionModelContainer())
                {
                    db.Results.Add(new Results()
                    {
                        ClassId = max_ind1,
                        Probability = max_val1,
                        FileHash = tensorHash,
                        Filename = filename,
                        Blob = new Blobs()
                        {
                            FileContent = fileBytes
                        },
                    });
                    db.SaveChanges();
                }
                curThreads--;
                if (curThreads <= threadLimit)
                {
                    limitHit.Set();
                }
                return new ImageClassified()
                {
                    ImagePath = filename,
                    ClassName = max_ind1.ToString(),
                    Certainty = max_val1,
                };
            }
            throw new Exception();
        }

        static private ImageClassified FindInDB(byte[] fileBytes)
        {
            ImageClassified result = null;
            var tensorHash = GetTensorHash(fileBytes);
            using (var db = new RecognitionModelContainer())
            {
                var query = from row in db.Results
                            where row.FileHash == tensorHash
                            select row;
                foreach (var row in query)
                {
                    row.HitCount++;
                    if (row.Blob.FileContent.Length == fileBytes.Length && row.Blob.FileContent.SequenceEqual(fileBytes))
                    {
                        result = new ImageClassified()
                        {
                            ImagePath = "",
                            ClassName = row.ClassId.ToString(), // here be translation to real class name later
                            Certainty = row.Probability,
                        };
                        break;
                    }
                }
                db.SaveChanges(); //is this necessary?..
            }
            return result;
        }

        static Int64 GetTensorHash(byte[] filebytes)
        {
            return new BigInteger(filebytes).GetHashCode();
        }

        static DenseTensor<float> LoadTensorFromFileBytes(byte[] fileBytes)
        {
            Bitmap tmp = new Bitmap(new MemoryStream(fileBytes));
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
