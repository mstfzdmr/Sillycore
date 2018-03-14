﻿using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sillycore;
using Sillycore.BackgroundProcessing;
using Sillycore.Web.Controllers;
using WebApplication.BackgroundJobs;
using WebApplication.Domain;

namespace WebApplication.Controllers
{
    //[Authorization("defaultPolicy")]
    [Route("samples")]
    public class SamplesController : SillyController
    {
        private readonly ILogger<SamplesController> _logger;
        private readonly IConfiguration _configuration;
        private readonly BackgroundJobManager _backgroundJobManager;

        public SamplesController(ILogger<SamplesController> logger, IConfiguration configuration, BackgroundJobManager backgroundJobManager)
        {
            _logger = logger;
            _configuration = configuration;
            _backgroundJobManager = backgroundJobManager;
        }

        [HttpGet("")]
        [ProducesResponseType(typeof(List<Sample>), (int)HttpStatusCode.OK)]
        public IActionResult QuerySamples(string name = null)
        {
            _logger.LogDebug("QuerySamples called.");

            List<Sample> samples = new List<Sample>();
            samples.Add(new Sample()
            {
                Id = Guid.NewGuid(),
                Name = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                CreatedOn = SillycoreApp.Instance.DateTimeProvider.Now
            });

            return Ok(samples);
        }

        [HttpPost("")]
        [ProducesResponseType(typeof(Sample), (int)HttpStatusCode.Created)]
        public IActionResult CreateSample([FromBody]Sample request)
        {
            return Created(request);
        }
    }
}