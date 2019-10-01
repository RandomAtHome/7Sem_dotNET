//variant a2
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics.Tensors;
using Microsoft.ML.OnnxRuntime;

namespace ParallelRecognition
{
    public class ParallelRecognition
    {
        private string directoryPath;
        public string DirectoryPath { get => directoryPath; set => directoryPath = value; }

        public bool HasFinished { get; private set; }

        public ParallelRecognition(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public bool Run()
        {
            var filePaths = Directory.GetFiles(DirectoryPath);
            foreach (var filePath in filePaths)
            {
                RecognizeContentsStub(filePath);
            }
            return true;
        }

        public bool Stop()
        {
            return true;
        }

        static string RecognizeContentsStub(string filePath)
        {
            return "{'Yay': 0}";
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
