using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using ADHealthChecker.Models;

namespace ADHealthChecker.Controllers;

public class HealthController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult RunAssessment()
    {
        // Execute commands
        var dcdiagQ = ExecuteCommand("dcdiag /q");
        var repadminOut = ExecuteCommand("repadmin /replsummary");
        var dcdiagDns = ExecuteCommand("dcdiag /test:dns");

        // DNS Health (use dcdiag /test:dns output)
        var dnsStatus = "Healthy";
        var dnsScore = 100;
        if (!string.IsNullOrEmpty(dcdiagDns) && (dcdiagDns.IndexOf("fail", System.StringComparison.OrdinalIgnoreCase) >= 0 || dcdiagDns.IndexOf("error", System.StringComparison.OrdinalIgnoreCase) >= 0))
        {
            dnsStatus = "Critical";
            dnsScore = 50;
        }

        // Replication (use repadmin output)
        var replStatus = "Healthy";
        var replScore = 100;
        if (!string.IsNullOrEmpty(repadminOut) && (repadminOut.IndexOf("fails", System.StringComparison.OrdinalIgnoreCase) >= 0 || repadminOut.IndexOf("error", System.StringComparison.OrdinalIgnoreCase) >= 0))
        {
            replStatus = "Critical";
            replScore = 50;
        }

        // DCDIAG (dcdiag /q)
        var dcdiagStatus = "Healthy";
        var dcdiagScore = 100;
        if (!string.IsNullOrEmpty(dcdiagQ))
        {
            dcdiagStatus = "Warning";
            dcdiagScore = 80;
        }

        // Prepare results
        var results = new List<HealthResult>
        {
            new HealthResult { CheckName = "DNS Health", Status = dnsStatus, Score = dnsScore, Recommendation = dnsStatus == "Critical" ? "Investigate DNS errors in dcdiag output." : "" },
            new HealthResult { CheckName = "Replication", Status = replStatus, Score = replScore, Recommendation = replStatus == "Critical" ? "Investigate replication failures with repadmin." : "" },
            new HealthResult { CheckName = "DCDIAG", Status = dcdiagStatus, Score = dcdiagScore, Recommendation = dcdiagStatus == "Warning" ? "Run full dcdiag for details." : "" }
        };

        // Overall health score = average of checks
        var overall = (dnsScore + replScore + dcdiagScore) / 3;
        ViewBag.OverallHealthScore = overall;

        // Also pass raw outputs for debugging if needed
        ViewBag.DcdiagQOutput = dcdiagQ;
        ViewBag.RepadminOutput = repadminOut;
        ViewBag.DcdiagDnsOutput = dcdiagDns;

        return View("Index", results);
    }

    private string ExecuteCommand(string command)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null)
                return string.Empty;

            var output = proc.StandardOutput.ReadToEnd();
            var err = proc.StandardError.ReadToEnd();
            proc.WaitForExit(60000);

            var combined = (output ?? string.Empty) + (err ?? string.Empty);
            return combined.Trim();
        }
        catch (System.Exception ex)
        {
            return "ERROR_EXECUTING_COMMAND: " + ex.Message;
        }
    }
}
