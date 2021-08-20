// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.TemplateDiscovery.Nuget;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.Results;
using Microsoft.TemplateSearch.TemplateDiscovery.Test;

namespace Microsoft.TemplateSearch.TemplateDiscovery
{
    internal class Program
    {
        private const bool _defaultRunOnlyOnePage = false;
        private const int _defaultPageSize = 100;
        private const bool _defaultSaveCandidatePacks = false;
        private const bool _defaultIncludePreviewPacks = false;

        private const string _basePathFlag = "--basePath";
        private const string _pageSizeFlag = "--pageSize";
        private const string _includePreviewPacksFlag = "--allowPreviewPacks";
        private const string _saveDownloadedPacksFlag = "--savePacks";
        private const string _runOnlyOnePageFlag = "--onePage";
        private const string _previousOutputBasePathFlag = "--previousOutput";
        private const string _noTemplateJsonFilterFlag = "--noTemplateJsonFilter";
        private const string _verbose = "-v";
        private const string _providers = "--providers";
        private const string _packagesPath = "--packagesPath";
        private const string _runTests = "-test";

        private static async Task Main(string[] args)
        {
            // setup the config with defaults
            ScraperConfig config = new ScraperConfig()
            {
                PageSize = _defaultPageSize,
                RunOnlyOnePage = _defaultRunOnlyOnePage,
                SaveCandidatePacks = _defaultSaveCandidatePacks,
                IncludePreviewPacks = _defaultIncludePreviewPacks
            };

            if (!TryParseArgs(args, config) || string.IsNullOrEmpty(config.BasePath))
            {
                // base path is the only required arg.
                ShowUsageMessage();
                return;
            }

            PackSourceChecker? packSourceChecker;

            // if or when we add other sources to scrape, input args can control which execute(s).
            if (true)
            {
                if (!NugetPackScraper.TryCreateDefaultNugetPackScraper(config, out packSourceChecker) || packSourceChecker == null)
                {
                    Console.WriteLine("Unable to create the NugetPackScraper.");
                    return;
                }
            }
            else
            {
                throw new NotImplementedException("no checker for the input options");
            }

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Canceling...");
                cts.Cancel();
                e.Cancel = true;
            };

            try
            {
                PackSourceCheckResult checkResults = await packSourceChecker.CheckPackagesAsync(cts.Token).ConfigureAwait(false);
                (string metadataPath, string legacyMetadataPath) = PackCheckResultReportWriter.WriteResults(config.BasePath, checkResults);

                if (config.RunValidationTests)
                {
                    CacheFileTests.RunTests(legacyMetadataPath);
                    CacheFileV2Tests.RunTests(metadataPath);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Operation was cancelled.");
            }
        }

        private static void ShowUsageMessage()
        {
            Console.WriteLine("Invalid inputs");
            Console.WriteLine();
            Console.WriteLine("Valid args:");
            Console.WriteLine($"{_basePathFlag} - The root dir for output for this run.");
            Console.WriteLine($"{_previousOutputBasePathFlag} - The root dir for output of a previous run. If specified, uses this output to filter packs known to not contain templates.");
            Console.WriteLine($"{_includePreviewPacksFlag} - Include preview packs in the results (by default, preview packs are ignored and the latest stable pack is used.");
            Console.WriteLine($"{_pageSizeFlag} - (debugging) The chunk size for interactions with the source.");
            Console.WriteLine($"{_runOnlyOnePageFlag} - (debugging) Only process one page of template packs.");
            Console.WriteLine($"{_saveDownloadedPacksFlag} - Don't delete downloaded candidate packs (by default, they're deleted at the end of a run).");
            Console.WriteLine($"{_noTemplateJsonFilterFlag} - Don't prefilter packs that don't contain any template.json files (this filter is applied by default).");
            Console.WriteLine($"{_verbose} - Verbose output for template processing).");
            Console.WriteLine($"{_providers} - bar separated list of providers to run. Supported providers: {string.Join(",", NugetPackScraper.SupportedProvidersList)}.");
            Console.WriteLine($"{_runTests} - Run validation tests after the metadata files are created.");
        }

        private static bool TryParseArgs(string[] args, ScraperConfig config)
        {
            int index = 0;

            while (index < args.Length)
            {
                if (string.Equals(args[index], _basePathFlag, StringComparison.Ordinal))
                {
                    if (TryGetFlagValue(args, index, out string? basePath) && !string.IsNullOrWhiteSpace(basePath))
                    {
                        config.BasePath = basePath;
                        index += 2;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (string.Equals(args[index], _packagesPath, StringComparison.Ordinal))
                {
                    if (TryGetFlagValue(args, index, out string? packagesPath) && !string.IsNullOrWhiteSpace(packagesPath))
                    {
                        if (!Directory.Exists(packagesPath))
                        {
                            Console.WriteLine($"Directory {packagesPath} does not exist.");
                            return false;
                        }

                        config.LocalPackagePath = packagesPath;
                        index += 2;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (string.Equals(args[index], _pageSizeFlag, StringComparison.Ordinal))
                {
                    if (TryGetFlagValue(args, index, out string? pageSizeString) && int.TryParse(pageSizeString, out int pageSize))
                    {
                        config.PageSize = pageSize;
                        index += 2;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (string.Equals(args[index], _previousOutputBasePathFlag, StringComparison.Ordinal))
                {
                    if (TryGetFlagValue(args, index, out string? previousOutputBasePath) && !string.IsNullOrWhiteSpace(previousOutputBasePath))
                    {
                        config.PreviousRunBasePath = previousOutputBasePath;
                        index += 2;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (string.Equals(args[index], _runOnlyOnePageFlag, StringComparison.Ordinal))
                {
                    config.RunOnlyOnePage = true;
                    ++index;
                }
                else if (string.Equals(args[index], _saveDownloadedPacksFlag, StringComparison.Ordinal))
                {
                    config.SaveCandidatePacks = true;
                    ++index;
                }
                else if (string.Equals(args[index], _includePreviewPacksFlag, StringComparison.Ordinal))
                {
                    config.IncludePreviewPacks = true;
                    ++index;
                }
                else if (string.Equals(args[index], _noTemplateJsonFilterFlag, StringComparison.Ordinal))
                {
                    config.DontFilterOnTemplateJson = true;
                    ++index;
                }
                else if (string.Equals(args[index], _verbose, StringComparison.Ordinal))
                {
                    Verbose.IsEnabled = true;
                    ++index;
                }
                else if (string.Equals(args[index], _providers, StringComparison.Ordinal))
                {
                    if (TryGetFlagValue(args, index, out string? providersList) && !string.IsNullOrWhiteSpace(providersList))
                    {
                        config.Providers.AddRange(providersList.Split('|', StringSplitOptions.TrimEntries));
                        foreach (string provider in config.Providers)
                        {
                            if (!NugetPackScraper.SupportedProvidersList.Contains(provider, StringComparer.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"Provider {provider} is not supported. Supported providers: {string.Join(",", NugetPackScraper.SupportedProvidersList)}.");
                                return false;
                            }
                        }
                        index += 2;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (string.Equals(args[index], _runTests, StringComparison.Ordinal))
                {
                    config.RunValidationTests = true;
                    ++index;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetFlagValue(string[] args, int index, out string? value)
        {
            if (index < args.Length && !args[index + 1].StartsWith("-"))
            {
                value = args[index + 1];
                return true;
            }

            value = null;
            return false;
        }
    }
}
