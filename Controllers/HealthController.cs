using System.Collections.Generic;
using System.Linq;
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
        var fsmoOut = ExecuteCommand("netdom query fsmo");
        var timeOut = ExecuteCommand("w32tm /query /status");
        var sysvolOut = ExecuteCommand("dcdiag /test:sysvolcheck");

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

        // FSMO Roles
        var fsmoStatus = "Healthy";
        var fsmoScore = 100;
        if (string.IsNullOrWhiteSpace(fsmoOut) || fsmoOut.IndexOf("error", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            fsmoStatus = "Warning";
            fsmoScore = 70;
        }

        // Time synchronization
        var timeStatus = "Healthy";
        var timeScore = 100;
        if (string.IsNullOrWhiteSpace(timeOut) || timeOut.IndexOf("source", System.StringComparison.OrdinalIgnoreCase) < 0)
        {
            timeStatus = "Warning";
            timeScore = 70;
        }

        // SYSVOL
        var sysvolStatus = "Healthy";
        var sysvolScore = 100;
        if (!string.IsNullOrEmpty(sysvolOut) && (sysvolOut.IndexOf("fail", System.StringComparison.OrdinalIgnoreCase) >= 0 || sysvolOut.IndexOf("error", System.StringComparison.OrdinalIgnoreCase) >= 0))
        {
            sysvolStatus = "Critical";
            sysvolScore = 50;
        }

        // Prepare results
        var results = new List<HealthResult>
        {
            new HealthResult { CheckName = "DNS Health", Status = dnsStatus, Score = dnsScore, Recommendation = dnsStatus == "Critical" ? "Investigate DNS errors in dcdiag output." : "", RawOutput = dcdiagDns },
            new HealthResult { CheckName = "Replication", Status = replStatus, Score = replScore, Recommendation = replStatus == "Critical" ? "Investigate replication failures with repadmin." : "", RawOutput = repadminOut },
            new HealthResult { CheckName = "DCDIAG", Status = dcdiagStatus, Score = dcdiagScore, Recommendation = dcdiagStatus == "Warning" ? "Run full dcdiag for details." : "", RawOutput = dcdiagQ },
            new HealthResult { CheckName = "FSMO Roles", Status = fsmoStatus, Score = fsmoScore, Recommendation = fsmoStatus != "Healthy" ? "Verify FSMO role holders." : "", RawOutput = fsmoOut },
            new HealthResult { CheckName = "Time Synchronization", Status = timeStatus, Score = timeScore, Recommendation = timeStatus != "Healthy" ? "Check NTP/time source configuration." : "", RawOutput = timeOut },
            new HealthResult { CheckName = "SYSVOL", Status = sysvolStatus, Score = sysvolScore, Recommendation = sysvolStatus == "Critical" ? "Check SYSVOL replication and permissions." : "", RawOutput = sysvolOut }
        };

        // Overall health score = average of checks
        var overall = (int)results.Average(r => r.Score);
        ViewBag.OverallHealthScore = overall;

        // Grade mapping
        string grade;
        if (overall >= 95) grade = "A+";
        else if (overall >= 90) grade = "A";
        else if (overall >= 80) grade = "B";
        else if (overall >= 70) grade = "C";
        else grade = "D";
        ViewBag.OverallGrade = grade;

        // Also pass raw outputs for debugging if needed
        ViewBag.DcdiagQOutput = dcdiagQ;
        ViewBag.RepadminOutput = repadminOut;
        ViewBag.DcdiagDnsOutput = dcdiagDns;
        ViewBag.FsmoOutput = fsmoOut;
        ViewBag.TimeOutput = timeOut;
        ViewBag.SysvolOutput = sysvolOut;

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
