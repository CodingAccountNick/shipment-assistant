using Microsoft.AspNetCore.Mvc;
using Shipments.Api.Services;


namespace Shipments.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DemoController(DemoWorker worker) : ControllerBase
    {
        // POST api/demo/start
        // Starts all workers and returns straight away. Watch the console for answers as they arrive.
        [HttpPost("start")]
        public ActionResult<IReadOnlyList<DemoJob>> Start() => Accepted(worker.Start());

        // GET api/demo/jobs
        // Current state of every worker.
        [HttpGet("jobs")]
        public ActionResult<IReadOnlyList<DemoJob>> Jobs() => Ok(worker.Jobs);
    }
}
