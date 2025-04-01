using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public class PowerPlanService : IPowerPlanService
    {
        private const string PowerPlansPath = @"C:\Users\Administrator\Desktop\Project\ThreadPilot_1\Powerplans";

        public async Task<ObservableCollection<PowerPlanModel>> GetPowerPlansAsync()
        {
            return await Task.Run(async () =>
            {
                var powerPlans = new ObservableCollection<PowerPlanModel>();
                var activePlan = await GetActivePowerPlan();

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = "/list",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var regex = new Regex(@"Power Scheme GUID: (.*?)  \((.*?)\)", RegexOptions.Multiline);
                var matches = regex.Matches(output);

                foreach (Match match in matches)
                {
                    var guid = match.Groups[1].Value.Trim();
                    var name = match.Groups[2].Value.Trim();

                    var plan = new PowerPlanModel
                    {
                        Guid = guid,
                        Name = name,
                        IsActive = guid == activePlan?.Guid,
                        IsCustomPlan = false
                    };

                    powerPlans.Add(plan);
                }

                return powerPlans;
            });
        }

        public async Task<ObservableCollection<PowerPlanModel>> GetCustomPowerPlansAsync()
        {
            return await Task.Run(() =>
            {
                var customPlans = new ObservableCollection<PowerPlanModel>();
                if (!Directory.Exists(PowerPlansPath))
                    return customPlans;

                foreach (var file in Directory.GetFiles(PowerPlansPath, "*.pow"))
                {
                    customPlans.Add(new PowerPlanModel
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        IsCustomPlan = true
                    });
                }

                return customPlans;
            });
        }

        public async Task<bool> SetActivePowerPlan(PowerPlanModel powerPlan)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powercfg",
                            Arguments = $"/setactive {powerPlan.Guid}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                            Verb = "runas" // Run with elevated privileges
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    return process.ExitCode == 0;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public async Task<PowerPlanModel?> GetActivePowerPlan()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powercfg",
                            Arguments = "/getactivescheme",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    var regex = new Regex(@"Power Scheme GUID: (.*?)  \((.*?)\)", RegexOptions.Multiline);
                    var match = regex.Match(output);

                    if (match.Success)
                    {
                        return new PowerPlanModel
                        {
                            Guid = match.Groups[1].Value.Trim(),
                            Name = match.Groups[2].Value.Trim(),
                            IsActive = true
                        };
                    }

                    return null;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        public async Task<bool> ImportCustomPowerPlan(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powercfg",
                            Arguments = $"/import \"{filePath}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                            Verb = "runas" // Run with elevated privileges
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    return process.ExitCode == 0;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }
    }
}