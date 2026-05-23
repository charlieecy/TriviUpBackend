using Microsoft.AspNetCore.Mvc;

namespace TriviUpBackend.Controllers;

[ApiController]
[Route("[controller]")]
public class TestsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly string _coveragePath;

    public TestsController(IWebHostEnvironment env)
    {
        _env = env;
        _coveragePath = Path.Combine(AppContext.BaseDirectory, "coverage", "index.html");
    }

    [HttpGet]
    public IActionResult GetReport()
    {
        var reportPath = Path.GetFullPath(_coveragePath);

        if (!System.IO.File.Exists(reportPath))
        {
            return Content(@"
<!DOCTYPE html>
<html><head><title>Tests Report</title>
<style>body{font-family:Arial;padding:20px;background:#f5f5f5}
.container{background:white;border-radius:8px;padding:20px;max-width:800px;margin:0 auto}
h1{color:#333}.warning{color:#856404;background:#fff3cd;border-radius:4px;padding:15px;border:1px solid #ffeaa7}
</style></head>
<body>
<div class='container'>
<h1>📊 Test Coverage Report</h1>
<div class='warning'>
<strong>⚠️ Report not found.</strong><br/>
Run the following command to generate the report:<br/>
<code>dotnet test --collect:""XPlat Code Coverage""</code>
</div>
</div>
</body></html>", "text/html");
        }

        return PhysicalFile(reportPath, "text/html");
    }
}