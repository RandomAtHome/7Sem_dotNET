using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace RESTService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RecognizeController : ControllerBase
    {
        [HttpGet]
        public DateTime Get()
        {
            return DateTime.Now;
        }
    }
}
