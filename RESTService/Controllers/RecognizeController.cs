using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CoreParallelRecognition;

namespace RESTService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RecognizeController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<ImageClassified> Get()
        {
            var dirPath = @"C:\Users\randomnb\Desktop\Pics2";
            var parallelRecognition = new ParallelRecognition(dirPath);
            parallelRecognition.Run();
            var images = new List<ImageClassified>(); 
            while (!parallelRecognition.HasFinished)
            {
                while (parallelRecognition.CreationTimes.TryDequeue(out ImageClassified item))
                {
                    images.Add(item);
                }
                Thread.Sleep(100);
            }
            return images;
        }
    }
}
